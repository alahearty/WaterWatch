using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Security;

namespace WaterWatch.Services;

public interface IFloodService
{
    Task<IReadOnlyList<FloodLayerDto>> GetLayersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FloodEventDto>> GetEventsAsync(CancellationToken cancellationToken = default);
    Task<FloodLayerDto> CreateLayerAsync(FloodLayerCreateRequest request, CancellationToken cancellationToken = default);
    Task<FloodEventDto> CreateEventAsync(FloodEventCreateRequest request, CancellationToken cancellationToken = default);
    Task<FloodExposureDto> AnalyseExposureAsync(Guid floodLayerId, string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FloodExposureDto>> GetExposureResultsAsync(CancellationToken cancellationToken = default);
}

public class FloodService : IFloodService
{
    private readonly DatabaseContext _db;
    private readonly IGeoJsonService _geoJson;
    private readonly IHttpContextAccessor _http;

    public FloodService(DatabaseContext db, IGeoJsonService geoJson, IHttpContextAccessor http)
    {
        _db = db;
        _geoJson = geoJson;
        _http = http;
    }

    private bool CanViewSensitive =>
        _http.HttpContext?.User is { Identity.IsAuthenticated: true } user &&
        Policies.Definitions.First(p => p.Name == Policies.CanViewSensitive).Roles.Any(user.IsInRole);

    public async Task<IReadOnlyList<FloodLayerDto>> GetLayersAsync(CancellationToken cancellationToken = default)
    {
        var layers = await _db.FloodLayers.AsNoTracking().Include(f => f.DataSource)
            .OrderByDescending(f => f.UpdatedAt).ToListAsync(cancellationToken);
        return layers.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<FloodEventDto>> GetEventsAsync(CancellationToken cancellationToken = default) =>
        await _db.FloodEvents.AsNoTracking().OrderByDescending(e => e.StartedAt)
            .Select(e => new FloodEventDto(e.Id, e.Name, e.StartedAt, e.EndedAt, e.Severity,
                e.Description, e.Source, e.IsOfficial, e.DataClassification))
            .ToListAsync(cancellationToken);

    public async Task<FloodLayerDto> CreateLayerAsync(FloodLayerCreateRequest request, CancellationToken cancellationToken = default)
    {
        Geometry? geometry = null;
        if (!string.IsNullOrWhiteSpace(request.GeometryGeoJson))
        {
            geometry = _geoJson.ParseGeometry(request.GeometryGeoJson);
            if (geometry is null)
            {
                throw new ArgumentException("The supplied flood layer geometry is not valid GeoJSON.");
            }
            var errors = _geoJson.Validate(geometry);
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join(" ", errors));
            }
        }
        if (request.IsOfficial && geometry is null)
        {
            throw new ArgumentException("An official flood layer requires geometry.");
        }

        DataSource? source = null;
        if (!string.IsNullOrWhiteSpace(request.SourceName))
        {
            source = await _db.DataSources.FirstOrDefaultAsync(d => d.Name == request.SourceName, cancellationToken);
            if (source is null)
            {
                source = new DataSource
                {
                    Name = request.SourceName, Publisher = request.SourceName,
                    Licence = null, IsOfficial = request.IsOfficial,
                    QualityNotes = "Source registered by an operator; licence must be verified before publication.",
                    DataClassification = request.IsOfficial ? DataClassification.Imported : DataClassification.Modelled
                };
                _db.DataSources.Add(source);
            }
        }

        var layer = new FloodLayer
        {
            Name = request.Name.Trim(), LayerType = request.LayerType, Description = request.Description,
            Geometry = geometry, FloodEventId = request.FloodEventId, ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo, IsOfficial = request.IsOfficial, Assumptions = request.Assumptions,
            Uncertainty = request.Uncertainty, ValidationStatus = request.ValidationStatus,
            ModelVersion = request.ModelVersion, DataSource = source,
            DataClassification = request.IsOfficial ? DataClassification.Imported :
                request.LayerType == FloodLayerType.ModelledSusceptibility ? DataClassification.Modelled :
                DataClassification.Observed
        };
        _db.FloodLayers.Add(layer);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(layer);
    }

    public async Task<FloodEventDto> CreateEventAsync(FloodEventCreateRequest request, CancellationToken cancellationToken = default)
    {
        var floodEvent = new FloodEvent
        {
            Name = request.Name.Trim(), StartedAt = request.StartedAt, EndedAt = request.EndedAt,
            Severity = request.Severity, Description = request.Description, Source = request.Source,
            IsOfficial = request.IsOfficial,
            DataClassification = request.IsOfficial ? DataClassification.Imported : DataClassification.Observed
        };
        _db.FloodEvents.Add(floodEvent);
        await _db.SaveChangesAsync(cancellationToken);
        return new FloodEventDto(floodEvent.Id, floodEvent.Name, floodEvent.StartedAt, floodEvent.EndedAt,
            floodEvent.Severity, floodEvent.Description, floodEvent.Source, floodEvent.IsOfficial, floodEvent.DataClassification);
    }

    public async Task<FloodExposureDto> AnalyseExposureAsync(Guid floodLayerId, string userName, CancellationToken cancellationToken = default)
    {
        var layer = await _db.FloodLayers.Include(f => f.DataSource)
            .FirstOrDefaultAsync(f => f.Id == floodLayerId, cancellationToken)
            ?? throw new KeyNotFoundException("Flood layer not found.");
        if (layer.Geometry is null)
        {
            throw new InvalidOperationException("The flood layer has no geometry; exposure cannot be calculated.");
        }

        var assets = await _db.Assets.AsNoTracking()
            .Where(a => a.Geometry != null && a.Geometry.Intersects(layer.Geometry))
            .ToListAsync(cancellationToken);
        var stations = await _db.SamplingStations.AsNoTracking().CountAsync(s => s.Geometry != null && s.Geometry.Intersects(layer.Geometry), cancellationToken);
        var devices = await _db.Devices.AsNoTracking().CountAsync(d => d.Geometry != null && d.Geometry.Intersects(layer.Geometry), cancellationToken);

        var visibleAssets = assets
            .Where(a => !a.IsSensitive || CanViewSensitive)
            .Select(a => new FloodAssetExposureDto(a.Id, a.AssetCode, a.Name, a.AssetType, a.Status,
                a.IsSensitive, a.IsSensitive && !CanViewSensitive ? null : a.Geometry?.Centroid.X,
                a.IsSensitive && !CanViewSensitive ? null : a.Geometry?.Centroid.Y))
            .ToList();

        var otherLayers = new List<FloodLayerExposureDto>();
        var candidateLayers = await _db.SpatialLayers.AsNoTracking()
            .Where(l => l.FeatureCount > 0 && l.Key != "osm-waterways" && l.Key != "osm-water-bodies" && l.Key != "osm-settlements")
            .ToListAsync(cancellationToken);
        foreach (var spatialLayer in candidateLayers)
        {
            var count = await _db.SpatialFeatures.AsNoTracking()
                .CountAsync(f => f.LayerId == spatialLayer.Id && f.Geometry.Intersects(layer.Geometry), cancellationToken);
            if (count > 0)
            {
                otherLayers.Add(new FloodLayerExposureDto(spatialLayer.Name, count));
            }
        }

        long? population = null;
        var intersectingAreas = await _db.AdminAreas.AsNoTracking()
            .Where(a => a.Population != null && a.Geometry != null && a.Geometry.Intersects(layer.Geometry))
            .ToListAsync(cancellationToken);
        if (intersectingAreas.Count > 0)
        {
            population = intersectingAreas.Sum(a => a.Population ?? 0);
        }

        var limitations = new List<string>
        {
            "Exposure is a geometric intersection, not a damage or casualty assessment.",
            "No building or road exposure layer is configured in this deployment; counts for such layers appear only if an operator imports them.",
            population is null ? "Population exposure is unavailable because no intersecting admin area has a population value." : "Population exposure is a simple sum of intersecting admin-area totals and is not spatially distributed."
        };
        if (layer.DataClassification == DataClassification.SyntheticDemonstration)
        {
            limitations.Add("This flood layer is synthetic demonstration data and must not be used for operational decisions.");
        }
        if (!layer.IsOfficial)
        {
            limitations.Add("This layer is not an official warning or forecast.");
        }

        var method = "PostGIS ST_Intersects between the flood layer geometry and asset/station/device point or geometry layers in EPSG:4326.";
        var results = new
        {
            assets = visibleAssets,
            otherLayers
        };

        var record = new FloodExposureResult
        {
            Name = $"Exposure — {layer.Name}",
            FloodLayerId = layer.Id,
            AnalysedAt = DateTimeOffset.UtcNow,
            AnalysedBy = userName,
            AssetsExposed = assets.Count,
            SamplingStationsExposed = stations,
            DevicesExposed = devices,
            PopulationExposed = population,
            Method = method,
            ParametersJson = JsonSerializer.Serialize(new { layerId = layer.Id, layerType = layer.LayerType.ToString() }),
            ResultsJson = JsonSerializer.Serialize(results),
            Limitations = string.Join(" ", limitations),
            DataClassification = layer.DataClassification
        };
        _db.FloodExposureResults.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        return new FloodExposureDto(record.Id, layer.Id, layer.Name, record.AnalysedAt, record.AnalysedBy,
            record.AssetsExposed, record.SamplingStationsExposed, record.DevicesExposed, record.PopulationExposed,
            visibleAssets, otherLayers, method, record.Limitations, record.DataClassification);
    }

    public async Task<IReadOnlyList<FloodExposureDto>> GetExposureResultsAsync(CancellationToken cancellationToken = default)
    {
        var records = await _db.FloodExposureResults.AsNoTracking().Include(r => r.FloodLayer)
            .OrderByDescending(r => r.AnalysedAt).Take(30).ToListAsync(cancellationToken);
        return records.Select(r =>
        {
            var assets = new List<FloodAssetExposureDto>();
            var otherLayers = new List<FloodLayerExposureDto>();
            try
            {
                using var document = JsonDocument.Parse(r.ResultsJson ?? "{}");
                if (document.RootElement.TryGetProperty("assets", out var assetsElement))
                {
                    assets = JsonSerializer.Deserialize<List<FloodAssetExposureDto>>(assetsElement.GetRawText()) ?? assets;
                }
                if (document.RootElement.TryGetProperty("otherLayers", out var layersElement))
                {
                    otherLayers = JsonSerializer.Deserialize<List<FloodLayerExposureDto>>(layersElement.GetRawText()) ?? otherLayers;
                }
            }
            catch (JsonException)
            {
                // Keep the persisted summary even if detail JSON is unreadable.
            }
            return new FloodExposureDto(r.Id, r.FloodLayerId, r.FloodLayer.Name, r.AnalysedAt, r.AnalysedBy,
                r.AssetsExposed, r.SamplingStationsExposed, r.DevicesExposed, r.PopulationExposed,
                assets, otherLayers, r.Method ?? string.Empty, r.Limitations ?? string.Empty, r.DataClassification);
        }).ToList();
    }

    private static FloodLayerDto ToDto(FloodLayer layer)
    {
        var validated = layer.ValidationStatus is not null &&
                        (layer.ValidationStatus.Contains("validated", StringComparison.OrdinalIgnoreCase) ||
                         layer.ValidationStatus.Contains("official", StringComparison.OrdinalIgnoreCase));
        var eligible = layer.IsOfficial && validated &&
                       layer.LayerType is FloodLayerType.OfficialWarning or FloodLayerType.Observed or FloodLayerType.Forecast;
        var note = eligible
            ? "Official and validated; can be presented as an official warning source."
            : !layer.IsOfficial
                ? "Not an official warning source; may only be shown as historical/modelled/observed intelligence."
                : layer.LayerType == FloodLayerType.ModelledSusceptibility
                    ? "Modelled susceptibility is not a forecast and cannot be issued as an operational alert."
                    : "Official status is recorded but validation is not evidenced; do not issue operational alerts.";
        return new FloodLayerDto(layer.Id, layer.Name, layer.LayerType, layer.Description,
            layer.ValidFrom, layer.ValidTo, layer.IsOfficial, layer.Assumptions, layer.Uncertainty,
            layer.ValidationStatus, layer.ModelVersion, layer.DataSource?.Name, layer.DataClassification,
            eligible, note, layer.UpdatedAt);
    }
}
