using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface IRemoteSensingService
{
    Task<StacSearchResultDto> SearchScenesAsync(StacSearchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChangeLayerDto>> GetChangeLayersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SatelliteSceneDto>> GetCachedScenesAsync(CancellationToken cancellationToken = default);
}

public class RemoteSensingService : IRemoteSensingService
{
    private readonly DatabaseContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public RemoteSensingService(DatabaseContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<StacSearchResultDto> SearchScenesAsync(StacSearchRequest request, CancellationToken cancellationToken = default)
    {
        var collection = request.Collection is "landsat-c2-l2" ? "landsat-c2-l2" : "sentinel-2-l2a";
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? to.AddMonths(-3);
        if (from > to)
        {
            throw new ArgumentException("The start date must be before the end date.");
        }
        var config = _configuration["WaterWatch:ExternalData:EarthSearchStacUrl"] ?? "https://earth-search.aws.element84.com/v1";
        var payload = new Dictionary<string, object?>
        {
            ["collections"] = new[] { collection },
            ["bbox"] = new[] { 6.89, 4.71, 7.16, 4.95 },
            ["datetime"] = $"{from:yyyy-MM-dd}T00:00:00Z/{to:yyyy-MM-dd}T23:59:59Z",
            ["limit"] = Math.Clamp(request.Limit, 1, 50),
            ["query"] = new Dictionary<string, object>
            {
                ["eo:cloud_cover"] = new Dictionary<string, object> { ["lt"] = Math.Clamp(request.MaxCloudPercent, 0, 100) }
            }
        };

        try
        {
            var client = _httpClientFactory.CreateClient("stac");
            var searchUrl = config.TrimEnd('/') + "/search";
            using var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, searchUrl) { Content = content };
            httpRequest.Headers.UserAgent.ParseAdd("WaterWatch/1.0 (Port Harcourt water intelligence)");
            httpRequest.Headers.Accept.ParseAdd("application/geo+json");
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (root.TryGetProperty("features", out var featuresElement))
            {
                var scenes = new List<SatelliteSceneDto>();
                foreach (var feature in featuresElement.EnumerateArray())
                {
                    var scene = ParseStacFeature(feature, collection);
                    if (scene is null)
                    {
                        continue;
                    }
                    scenes.Add(scene);
                    await CacheSceneAsync(scene, cancellationToken);
                }
                return new StacSearchResultDto("available",
                    scenes.Count == 0 ? "No scenes matched the date/cloud filters." : null,
                    collection, DateTimeOffset.UtcNow, scenes.OrderByDescending(s => s.AcquiredAt).ToList(),
                    "Element 84 Earth Search STAC");
            }
            return new StacSearchResultDto("unavailable", "The STAC response did not contain a features array.",
                collection, DateTimeOffset.UtcNow, Array.Empty<SatelliteSceneDto>(), "Element 84 Earth Search STAC");
        }
        catch (Exception ex)
        {
            return new StacSearchResultDto("unavailable",
                $"The STAC catalogue could not be reached: {ex.Message}. No cached scenes are presented as current.",
                collection, DateTimeOffset.UtcNow, await GetCachedScenesAsync(cancellationToken),
                "Element 84 Earth Search STAC");
        }
    }

    public async Task<IReadOnlyList<SatelliteSceneDto>> GetCachedScenesAsync(CancellationToken cancellationToken = default) =>
        await _db.SatelliteScenes.AsNoTracking().OrderByDescending(s => s.AcquiredAt).Take(200)
            .Select(s => new SatelliteSceneDto(s.Provider, s.Collection, s.SceneId, s.AcquiredAt,
                s.CloudCover, s.ResolutionM, s.ThumbnailUrl, null, s.SourceUrl,
                Array.Empty<double>(), s.Licence ?? string.Empty, s.DataClassification))
            .ToListAsync(cancellationToken);

    private async Task CacheSceneAsync(SatelliteSceneDto scene, CancellationToken cancellationToken)
    {
        var existing = await _db.SatelliteScenes
            .FirstOrDefaultAsync(s => s.Provider == scene.Provider && s.SceneId == scene.SceneId, cancellationToken);
        if (existing is null)
        {
            existing = new SatelliteScene
            {
                Provider = scene.Provider, Collection = scene.Collection, SceneId = scene.SceneId,
                AcquiredAt = scene.AcquiredAt, CloudCover = scene.CloudCover, ResolutionM = scene.ResolutionM,
                ThumbnailUrl = scene.ThumbnailUrl, SourceUrl = scene.SourceUrl, Licence = scene.Licence,
                DataClassification = DataClassification.Imported, RetrievedAt = DateTimeOffset.UtcNow,
                AssetsJson = JsonSerializer.Serialize(new { scene.VisualUrl })
            };
            if (scene.BoundingBox.Count == 4)
            {
                var envelope = new Envelope(scene.BoundingBox[0], scene.BoundingBox[2], scene.BoundingBox[1], scene.BoundingBox[3]);
                existing.Footprint = SpatialMath.Wgs84Factory.ToGeometry(envelope);
                existing.Footprint.SRID = SpatialMath.Wgs84;
            }
            _db.SatelliteScenes.Add(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ChangeLayerDto>> GetChangeLayersAsync(CancellationToken cancellationToken = default)
    {
        var layers = await _db.SpatialLayers.AsNoTracking().Include(l => l.DataSource)
            .Where(l => l.Category.Contains("change") || l.Key.StartsWith("rs-change"))
            .ToListAsync(cancellationToken);
        var result = new List<ChangeLayerDto>();
        foreach (var layer in layers)
        {
            var features = await _db.SpatialFeatures.AsNoTracking().Where(f => f.LayerId == layer.Id).ToListAsync(cancellationToken);
            var rows = new List<(string ClassName, double Area)>();
            foreach (var feature in features)
            {
                var className = ExtractClass(feature.AttributesJson);
                rows.Add((className, SpatialMath.AreaSquareKilometers(feature.Geometry)));
            }
            var total = rows.Sum(r => r.Area);
            var classes = rows.GroupBy(r => r.ClassName).Select(g => new ChangeClassStatDto(
                g.Key, Math.Round(g.Sum(r => r.Area), 3), g.Count(),
                total == 0 ? null : Math.Round((decimal)(g.Sum(r => r.Area) / total * 100), 1))).ToList();
            result.Add(new ChangeLayerDto(layer.Key, layer.Name, layer.Description, null, null,
                layer.Category, layer.DataSource?.Name ?? "Unknown", layer.DataSource?.Licence,
                Math.Round(total, 3), classes.OrderByDescending(c => c.AreaSqKm).ToList(),
                layer.UpdatedAt, layer.DataClassification));
        }
        return result;
    }

    private static SatelliteSceneDto? ParseStacFeature(JsonElement feature, string collection)
    {
        if (!feature.TryGetProperty("id", out var idElement))
        {
            return null;
        }
        var sceneId = idElement.GetString() ?? string.Empty;
        var properties = feature.TryGetProperty("properties", out var p) ? p : default;
        DateTimeOffset acquired = DateTimeOffset.MinValue;
        if (properties.ValueKind == JsonValueKind.Object &&
            properties.TryGetProperty("datetime", out var dateElement) &&
            DateTimeOffset.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            acquired = parsed;
        }
        decimal? cloud = null;
        if (properties.ValueKind == JsonValueKind.Object &&
            properties.TryGetProperty("eo:cloud_cover", out var cloudElement) &&
            cloudElement.ValueKind == JsonValueKind.Number)
        {
            cloud = cloudElement.GetDecimal();
        }
        string? thumbnail = null;
        string? visual = null;
        if (feature.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Object)
        {
            thumbnail = AssetHref(assets, "thumbnail") ?? AssetHref(assets, "rendered_preview");
            visual = AssetHref(assets, "visual") ?? AssetHref(assets, "rendered_preview");
        }
        var bbox = new List<double>();
        if (feature.TryGetProperty("bbox", out var bboxElement) && bboxElement.ValueKind == JsonValueKind.Array)
        {
            bbox.AddRange(bboxElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetDouble()).Take(4));
        }
        var resolution = collection == "landsat-c2-l2" ? 30m : 10m;
        return new SatelliteSceneDto("Element 84 Earth Search", collection, sceneId, acquired, cloud, resolution,
            thumbnail, visual, $"https://earth-search.aws.element84.com/v1/collections/{collection}/items/{sceneId}",
            bbox, collection == "landsat-c2-l2" ? "USGS Landsat open data" : "ESA Copernicus Sentinel open data",
            DataClassification.Imported);
    }

    private static string? AssetHref(JsonElement assets, string key)
    {
        if (assets.TryGetProperty(key, out var asset) && asset.ValueKind == JsonValueKind.Object &&
            asset.TryGetProperty("href", out var href))
        {
            return href.GetString();
        }
        return null;
    }

    private static string ExtractClass(string? attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
        {
            return "Unclassified";
        }
        try
        {
            using var document = JsonDocument.Parse(attributesJson);
            foreach (var key in new[] { "change_class", "class", "landcover", "land_use", "water_change", "category" })
            {
                if (document.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? "Unclassified";
                }
            }
        }
        catch (JsonException)
        {
            // fall through
        }
        return "Unclassified";
    }
}
