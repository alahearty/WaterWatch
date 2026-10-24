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

public interface IGeographyService
{
    Task<IReadOnlyList<LayerDto>> GetLayersAsync(CancellationToken cancellationToken = default);
    Task<GeographyContextDto> GetContextAsync(CancellationToken cancellationToken = default);
    Task<string> GetLayerGeoJsonAsync(string key, LayerFeatureQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataSourceDto>> GetDataSourcesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminAreaDto>> GetAdminAreasAsync(AdminLevel? level, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperationalAreaDto>> GetOperationalAreasAsync(OperationalAreaType? type, CancellationToken cancellationToken = default);
    MapExtentDto? GetExtent(CancellationToken cancellationToken = default);
}

public class GeographyService : IGeographyService
{
    private readonly DatabaseContext _db;
    private readonly IGeoJsonService _geoJson;
    private readonly IHttpContextAccessor _http;
    private readonly IConfiguration _configuration;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public GeographyService(DatabaseContext db, IGeoJsonService geoJson, IHttpContextAccessor http, IConfiguration configuration)
    {
        _db = db;
        _geoJson = geoJson;
        _http = http;
        _configuration = configuration;
    }

    private bool CanViewSensitive =>
        _http.HttpContext?.User is { Identity.IsAuthenticated: true } user &&
        Policies.Definitions.First(p => p.Name == Policies.CanViewSensitive).Roles.Any(user.IsInRole);

    private bool IsAuthenticated => _http.HttpContext?.User.Identity?.IsAuthenticated == true;

    public async Task<IReadOnlyList<LayerDto>> GetLayersAsync(CancellationToken cancellationToken = default)
    {
        var layers = new List<LayerDto>();
        var adminCounts = await _db.AdminAreas.AsNoTracking()
            .GroupBy(a => a.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        LayerDto AdminLayer(string key, string name, AdminLevel level, bool visible, string colour) =>
            new(key, name, "Administrative", "MultiPolygon",
                adminCounts.FirstOrDefault(c => c.Level == level)?.Count ?? 0,
                true, visible,
                JsonSerializer.Serialize(new { color = colour, weight = 1.4, fillOpacity = 0.06 }, JsonOptions),
                DataClassification.Imported, "OCHA COD-AB Nigeria", "CC BY-IGO");

        layers.Add(AdminLayer("admin-country", "Nigeria", AdminLevel.Country, false, "#475569"));
        layers.Add(AdminLayer("admin-state", "Rivers State", AdminLevel.State, false, "#0f766e"));
        layers.Add(AdminLayer("admin-lga", "Port Harcourt & Obio/Akpor LGAs", AdminLevel.Lga, true, "#0ea5e9"));

        var featureCounts = await _db.SpatialFeatures.AsNoTracking()
            .GroupBy(f => f.LayerId).Select(g => new { LayerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.LayerId, g => g.Count, cancellationToken);
        var zoneCount = await _db.OperationalAreas.CountAsync(cancellationToken);
        if (zoneCount > 0)
        {
            layers.Add(new LayerDto(
                "operational-zones", "Service zones, DMAs and catchments", "Operations", "MultiPolygon",
                zoneCount, true, false,
                JsonSerializer.Serialize(new { color = "#7c3aed", weight = 2, dashArray = "6 4", fillOpacity = 0.12 }, JsonOptions),
                DataClassification.SyntheticDemonstration, "WaterWatch demonstration", "Internal demonstration data"));
        }

        var assetCount = await _db.Assets.CountAsync(cancellationToken);
        layers.Add(new LayerDto(
            "assets", "Water infrastructure assets", "Infrastructure", "Geometry",
            assetCount, true, true,
            JsonSerializer.Serialize(new { color = "#0284c7", weight = 2, fillOpacity = 0.65 }, JsonOptions),
            DataClassification.Observed, "WaterWatch asset registry", "Internal"));

        foreach (var key in new[] { "osm-waterways", "osm-water-bodies", "osm-settlements" })
        {
            var layer = await _db.SpatialLayers.AsNoTracking()
                .Include(l => l.DataSource)
                .FirstOrDefaultAsync(l => l.Key == key, cancellationToken);
            if (layer is not null)
            {
                layers.Add(new LayerDto(
                    layer.Key, layer.Name, layer.Category, layer.GeometryType,
                    featureCounts.GetValueOrDefault(layer.Id), layer.IsPublic, key == "osm-waterways",
                    layer.StyleJson, layer.DataClassification,
                    layer.DataSource?.Name, layer.DataSource?.Licence));
            }
        }

        var stationCount = await _db.SamplingStations.CountAsync(cancellationToken);
        if (stationCount > 0)
        {
            layers.Add(new LayerDto("sampling-stations", "Water quality sampling stations", "Water quality", "Point",
                stationCount, true, false,
                JsonSerializer.Serialize(new { color = "#be123c", weight = 2, fillOpacity = 0.8 }, JsonOptions),
                DataClassification.Observed, "WaterWatch quality register", "Internal"));
        }

        var hydrologyCount = await _db.HydrologyStations.CountAsync(cancellationToken);
        if (hydrologyCount > 0)
        {
            layers.Add(new LayerDto("hydrology-stations", "Hydrology monitoring stations", "Hydrology", "Point",
                hydrologyCount, true, false,
                JsonSerializer.Serialize(new { color = "#0369a1", weight = 2, fillOpacity = 0.8 }, JsonOptions),
                DataClassification.Observed, "WaterWatch hydrology register", "Internal"));
        }

        var deviceCount = await _db.Devices.CountAsync(cancellationToken);
        if (deviceCount > 0)
        {
            layers.Add(new LayerDto("devices", "IoT monitoring devices", "Telemetry", "Point",
                deviceCount, true, false,
                JsonSerializer.Serialize(new { color = "#ca8a04", weight = 2, fillOpacity = 0.8 }, JsonOptions),
                DataClassification.Observed, "WaterWatch device registry", "Internal"));
        }

        var floodCount = await _db.FloodLayers.CountAsync(cancellationToken);
        if (floodCount > 0)
        {
            layers.Add(new LayerDto("flood-layers", "Flood layers (classified)", "Flood", "Geometry",
                floodCount, true, false,
                JsonSerializer.Serialize(new { color = "#1d4ed8", weight = 2, fillOpacity = 0.22 }, JsonOptions),
                DataClassification.Modelled, "See layer metadata", "See layer metadata"));
        }

        if (IsAuthenticated)
        {
            var workOrderCount = await _db.WorkOrders.CountAsync(cancellationToken);
            if (workOrderCount > 0)
            {
                layers.Add(new LayerDto("work-orders", "Field work orders", "Field operations", "Point",
                    workOrderCount, false, false,
                    JsonSerializer.Serialize(new { color = "#ea580c", weight = 2, fillOpacity = 0.8 }, JsonOptions),
                    DataClassification.Observed, "WaterWatch operations", "Internal"));
            }
        }

        var importedLayers = await _db.SpatialLayers.AsNoTracking()
            .Where(l => l.Key != "osm-waterways" && l.Key != "osm-water-bodies" && l.Key != "osm-settlements")
            .Include(l => l.DataSource)
            .OrderBy(l => l.Category).ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);

        foreach (var layer in importedLayers)
        {
            layers.Add(new LayerDto(layer.Key, layer.Name, layer.Category, layer.GeometryType,
                featureCounts.GetValueOrDefault(layer.Id), layer.IsPublic, layer.IsVisibleByDefault, layer.StyleJson, layer.DataClassification,
                layer.DataSource?.Name, layer.DataSource?.Licence,
                layer.ServiceType, layer.ServiceUrl, layer.ServiceLayerName));
        }

        return layers;
    }

    public async Task<GeographyContextDto> GetContextAsync(CancellationToken cancellationToken = default)
    {
        var lgas = await GetAdminAreasAsync(AdminLevel.Lga, cancellationToken);
        var geometries = await _db.AdminAreas.AsNoTracking()
            .Where(a => a.Level == AdminLevel.Lga && a.Geometry != null)
            .Select(a => a.Geometry!)
            .ToListAsync(cancellationToken);
        var extent = GetEnvelope(geometries);
        var demoMode = _configuration.GetValue("WaterWatch:DemoMode", false);
        return new GeographyContextDto(
            "Nigeria", "Rivers State", lgas, extent,
            _configuration.GetValue("WaterWatch:InitialLatitude", 4.8156),
            _configuration.GetValue("WaterWatch:InitialLongitude", 7.0498),
            _configuration.GetValue("WaterWatch:InitialZoom", 13),
            demoMode);
    }

    public async Task<string> GetLayerGeoJsonAsync(string key, LayerFeatureQuery query, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(query.Limit, 1, 10000);
        var features = new List<GeoJsonFeatureOutput>();
        var bbox = Bbox(query);

        switch (key)
        {
            case "admin-country":
            case "admin-state":
            case "admin-lga":
            {
                var level = key switch
                {
                    "admin-country" => AdminLevel.Country,
                    "admin-state" => AdminLevel.State,
                    _ => AdminLevel.Lga
                };
                var areas = await _db.AdminAreas.AsNoTracking()
                    .Where(a => a.Level == level && a.Geometry != null)
                    .OrderBy(a => a.Name)
                    .Take(limit)
                    .ToListAsync(cancellationToken);
                features.AddRange(areas.Where(a => bbox is null || a.Geometry!.Intersects(bbox)).Select(a => new GeoJsonFeatureOutput(
                    a.Id.ToString(), a.Geometry!, Props(
                        ("code", a.Code), ("name", a.Name), ("level", a.Level.ToString()),
                        ("areaSqKm", a.AreaSqKm), ("population", a.Population), ("populationYear", a.PopulationYear),
                        ("classification", a.DataClassification.ToString())))));
                break;
            }
            case "operational-zones":
            {
                var iquery = _db.OperationalAreas.AsNoTracking().Where(z => z.Geometry != null);
                if (query.Classification is not null)
                {
                    iquery = iquery.Where(z => z.DataClassification == query.Classification);
                }
                var zones = await iquery.OrderBy(z => z.Name).Take(limit).ToListAsync(cancellationToken);
                features.AddRange(zones.Where(z => bbox is null || z.Geometry!.Intersects(bbox)).Select(z => new GeoJsonFeatureOutput(
                    z.Id.ToString(), z.Geometry!, Props(
                        ("code", z.Code), ("name", z.Name), ("areaType", z.AreaType.ToString()),
                        ("areaSqKm", z.AreaSqKm), ("isOfficial", z.IsOfficial),
                        ("classification", z.DataClassification.ToString()), ("notes", z.Notes)))));
                break;
            }
            case "assets":
            {
                var iquery = _db.Assets.AsNoTracking().Include(a => a.AdminArea).Include(a => a.OperationalArea).Include(a => a.DataSource)
                    .Where(a => a.Geometry != null);
                if (!CanViewSensitive)
                {
                    iquery = iquery.Where(a => !a.IsSensitive);
                }
                if (query.AdminAreaId is not null)
                {
                    iquery = iquery.Where(a => a.AdminAreaId == query.AdminAreaId);
                }
                if (query.Classification is not null)
                {
                    iquery = iquery.Where(a => a.DataClassification == query.Classification);
                }
                if (query.AssetType is not null)
                {
                    iquery = iquery.Where(a => a.AssetType == query.AssetType);
                }
                if (query.Status is not null)
                {
                    iquery = iquery.Where(a => a.Status == query.Status);
                }
                var assets = await iquery.OrderBy(a => a.Name).Take(limit).ToListAsync(cancellationToken);
                foreach (var asset in assets.Where(a => bbox is null || a.Geometry!.Intersects(bbox)))
                {
                    var props = Props(
                        ("id", asset.Id), ("assetCode", asset.AssetCode), ("name", asset.Name),
                        ("assetType", asset.AssetType.ToString()), ("status", asset.Status.ToString()),
                        ("condition", asset.Condition.ToString()), ("adminArea", asset.AdminArea?.Name),
                        ("operationalArea", asset.OperationalArea?.Name), ("isSensitive", asset.IsSensitive),
                        ("classification", asset.DataClassification.ToString()), ("source", asset.DataSource?.Name));
                    if (asset.IsSensitive && !CanViewSensitive)
                    {
                        continue;
                    }
                    features.Add(new GeoJsonFeatureOutput(asset.Id.ToString(), asset.Geometry!, props));
                }
                break;
            }
            case "sampling-stations":
            {
                var stations = await _db.SamplingStations.AsNoTracking().Include(s => s.AdminArea)
                    .Where(s => s.Geometry != null)
                    .OrderBy(s => s.Name).Take(limit).ToListAsync(cancellationToken);
                features.AddRange(stations.Where(s => bbox is null || s.Geometry!.Intersects(bbox)).Select(s => new GeoJsonFeatureOutput(
                    s.Id.ToString(), s.Geometry!, Props(
                        ("id", s.Id), ("code", s.Code), ("name", s.Name),
                        ("waterSourceType", s.WaterSourceType.ToString()), ("adminArea", s.AdminArea?.Name),
                        ("isActive", s.IsActive), ("classification", s.DataClassification.ToString())))));
                break;
            }
            case "hydrology-stations":
            {
                var stations = await _db.HydrologyStations.AsNoTracking()
                    .Where(s => s.Geometry != null).OrderBy(s => s.Name).Take(limit).ToListAsync(cancellationToken);
                features.AddRange(stations.Where(s => bbox is null || s.Geometry!.Intersects(bbox)).Select(s => new GeoJsonFeatureOutput(
                    s.Id.ToString(), s.Geometry!, Props(
                        ("id", s.Id), ("code", s.Code), ("name", s.Name),
                        ("stationType", s.StationType.ToString()), ("waterway", s.WaterwayName),
                        ("classification", s.DataClassification.ToString())))));
                break;
            }
            case "devices":
            {
                var devices = await _db.Devices.AsNoTracking()
                    .Where(d => d.Geometry != null).OrderBy(d => d.Name).Take(limit).ToListAsync(cancellationToken);
                features.AddRange(devices.Where(d => bbox is null || d.Geometry!.Intersects(bbox)).Select(d => new GeoJsonFeatureOutput(
                    d.Id.ToString(), d.Geometry!, Props(
                        ("id", d.Id), ("deviceCode", d.DeviceCode), ("name", d.Name),
                        ("deviceType", d.DeviceType.ToString()), ("status", d.Status.ToString()),
                        ("isDemoDevice", d.IsDemoDevice), ("lastSeenAt", d.LastSeenAt),
                        ("classification", d.DataClassification.ToString())))));
                break;
            }
            case "flood-layers":
            {
                var floodLayers = await _db.FloodLayers.AsNoTracking().Include(f => f.DataSource)
                    .Where(f => f.Geometry != null).OrderBy(f => f.Name).Take(limit).ToListAsync(cancellationToken);
                features.AddRange(floodLayers.Where(f => bbox is null || f.Geometry!.Intersects(bbox)).Select(f => new GeoJsonFeatureOutput(
                    f.Id.ToString(), f.Geometry!, Props(
                        ("id", f.Id), ("name", f.Name), ("layerType", f.LayerType.ToString()),
                        ("isOfficial", f.IsOfficial), ("classification", f.DataClassification.ToString()),
                        ("validFrom", f.ValidFrom), ("validTo", f.ValidTo), ("assumptions", f.Assumptions),
                        ("uncertainty", f.Uncertainty), ("validationStatus", f.ValidationStatus),
                        ("source", f.DataSource?.Name)))));
                break;
            }
            case "work-orders":
            {
                if (!IsAuthenticated)
                {
                    break;
                }
                var orders = await _db.WorkOrders.AsNoTracking()
                    .Where(w => w.Geometry != null).OrderByDescending(w => w.CreatedAt).Take(limit).ToListAsync(cancellationToken);
                features.AddRange(orders.Where(w => bbox is null || w.Geometry!.Intersects(bbox)).Select(w => new GeoJsonFeatureOutput(
                    w.Id.ToString(), w.Geometry!, Props(
                        ("id", w.Id), ("reference", w.Reference), ("title", w.Title),
                        ("status", w.Status.ToString()), ("priority", w.Priority.ToString()),
                        ("type", w.Type.ToString()), ("assignedTo", w.AssignedTo)))));
                break;
            }
            default:
            {
                var layer = await _db.SpatialLayers.AsNoTracking().FirstOrDefaultAsync(l => l.Key == key, cancellationToken);
                if (layer is null)
                {
                    throw new KeyNotFoundException($"Layer '{key}' was not found.");
                }
                var spatialQuery = _db.SpatialFeatures.AsNoTracking().Where(f => f.LayerId == layer.Id);
                if (query.Classification is not null)
                {
                    spatialQuery = spatialQuery.Where(f => f.DataClassification == query.Classification);
                }
                var spatialFeatures = await spatialQuery.OrderBy(f => f.Name).Take(limit).ToListAsync(cancellationToken);
                features.AddRange(spatialFeatures.Where(f => bbox is null || f.Geometry.Intersects(bbox)).Select(f =>
                {
                    var props = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(f.AttributesJson))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(f.AttributesJson);
                            foreach (var property in doc.RootElement.EnumerateObject())
                            {
                                if (IsDisplayAttribute(property.Name))
                                {
                                    props[property.Name] = property.Value.ValueKind switch
                                    {
                                        JsonValueKind.String => property.Value.GetString(),
                                        JsonValueKind.Number => property.Value.TryGetInt64(out var l) ? l : property.Value.GetDouble(),
                                        JsonValueKind.True => true,
                                        JsonValueKind.False => false,
                                        _ => property.Value.GetRawText()
                                    };
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Keep the feature visible even when an attribute payload is malformed.
                        }
                    }
                    props["id"] = f.Id;
                    props["name"] = f.Name;
                    props["sourceUrl"] = f.SourceUrl;
                    props["classification"] = f.DataClassification.ToString();
                    return new GeoJsonFeatureOutput(f.Id.ToString(), f.Geometry, props);
                }));
                break;
            }
        }

        return _geoJson.SerializeFeatureCollection(features);
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length < 2)
        {
            return Array.Empty<SearchResultDto>();
        }

        var pattern = $"%{query}%";
        var results = new List<SearchResultDto>();

        var areas = await _db.AdminAreas.AsNoTracking()
            .Where(a => EF.Functions.ILike(a.Name, pattern) || (a.Alias != null && EF.Functions.ILike(a.Alias, pattern)))
            .Take(8).ToListAsync(cancellationToken);
        foreach (var area in areas)
        {
            var centroid = area.Geometry?.Centroid;
            results.Add(new SearchResultDto("Administrative area", area.Name,
                $"{area.Level} · {area.Code}", area.Id, centroid?.X, centroid?.Y, "admin-lga", null));
        }

        var assets = await _db.Assets.AsNoTracking()
            .Where(a => (EF.Functions.ILike(a.Name, pattern) || EF.Functions.ILike(a.AssetCode, pattern)) &&
                        (CanViewSensitive || !a.IsSensitive))
            .Take(10).ToListAsync(cancellationToken);
        foreach (var asset in assets)
        {
            var point = asset.Geometry?.Centroid;
            results.Add(new SearchResultDto("Asset", asset.Name,
                $"{asset.AssetType} · {asset.AssetCode}", asset.Id,
                asset.IsSensitive && !CanViewSensitive ? null : point?.X,
                asset.IsSensitive && !CanViewSensitive ? null : point?.Y,
                "assets", $"/Infrastructure/Details/{asset.Id}"));
        }

        var stations = await _db.SamplingStations.AsNoTracking()
            .Where(s => EF.Functions.ILike(s.Name, pattern) || EF.Functions.ILike(s.Code, pattern))
            .Take(6).ToListAsync(cancellationToken);
        results.AddRange(stations.Select(s => new SearchResultDto("Sampling station", s.Name,
            s.Code, s.Id, s.Geometry?.X, s.Geometry?.Y, "sampling-stations", $"/WaterQuality/Station/{s.Id}")));

        var hydrology = await _db.HydrologyStations.AsNoTracking()
            .Where(s => EF.Functions.ILike(s.Name, pattern) || EF.Functions.ILike(s.Code, pattern))
            .Take(6).ToListAsync(cancellationToken);
        results.AddRange(hydrology.Select(s => new SearchResultDto("Hydrology station", s.Name,
            $"{s.StationType} · {s.Code}", s.Id, s.Geometry?.X, s.Geometry?.Y, "hydrology-stations", null)));

        var devices = await _db.Devices.AsNoTracking()
            .Where(d => EF.Functions.ILike(d.Name, pattern) || EF.Functions.ILike(d.DeviceCode, pattern))
            .Take(6).ToListAsync(cancellationToken);
        results.AddRange(devices.Select(d => new SearchResultDto("Device", d.Name,
            $"{d.DeviceType} · {d.DeviceCode}", d.Id, d.Geometry?.X, d.Geometry?.Y, "devices", "/IoT")));

        if (IsAuthenticated)
        {
            var orders = await _db.WorkOrders.AsNoTracking()
                .Where(w => EF.Functions.ILike(w.Title, pattern) || EF.Functions.ILike(w.Reference, pattern))
                .Take(6).ToListAsync(cancellationToken);
            results.AddRange(orders.Select(w => new SearchResultDto("Work order", w.Title,
                $"{w.Reference} · {w.Status}", w.Id, w.Geometry?.X, w.Geometry?.Y, "work-orders", "/FieldOperations")));
        }

        return results;
    }

    public async Task<IReadOnlyList<DataSourceDto>> GetDataSourcesAsync(CancellationToken cancellationToken = default) =>
        await _db.DataSources.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DataSourceDto(d.Id, d.Name, d.Publisher, d.Licence, d.SourceUrl, d.AcquiredAt, d.QualityNotes, d.IsOfficial))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminAreaDto>> GetAdminAreasAsync(AdminLevel? level, CancellationToken cancellationToken = default)
    {
        var query = _db.AdminAreas.AsNoTracking();
        if (level is not null)
        {
            query = query.Where(a => a.Level == level);
        }
        return await query.OrderBy(a => a.Level).ThenBy(a => a.Name)
            .Select(a => new AdminAreaDto(a.Id, a.Code, a.Name, a.Alias, a.Level, a.ParentId,
                a.AreaSqKm, a.Population, a.PopulationYear, a.PopulationSource, a.DataClassification))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperationalAreaDto>> GetOperationalAreasAsync(OperationalAreaType? type, CancellationToken cancellationToken = default)
    {
        var query = _db.OperationalAreas.AsNoTracking();
        if (type is not null)
        {
            query = query.Where(a => a.AreaType == type);
        }
        return await query.OrderBy(a => a.AreaType).ThenBy(a => a.Name)
            .Select(a => new OperationalAreaDto(a.Id, a.Code, a.Name, a.AreaType, a.AreaSqKm,
                a.Population, a.IsOfficial, a.DataClassification, a.Notes))
            .ToListAsync(cancellationToken);
    }

    public MapExtentDto? GetExtent(CancellationToken cancellationToken = default)
    {
        var geometries = _db.AdminAreas.AsNoTracking()
            .Where(a => a.Level == AdminLevel.Lga && a.Geometry != null)
            .Select(a => a.Geometry!)
            .ToList();
        return GetEnvelope(geometries);
    }

    private static MapExtentDto? GetEnvelope(IEnumerable<Geometry> geometries)
    {
        Envelope? envelope = null;
        foreach (var geometry in geometries)
        {
            if (geometry.IsEmpty)
            {
                continue;
            }
            envelope ??= new Envelope(geometry.EnvelopeInternal);
            envelope.ExpandToInclude(geometry.EnvelopeInternal);
        }
        return envelope is null ? null : new MapExtentDto(envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY);
    }

    private static Geometry? Bbox(LayerFeatureQuery query)
    {
        if (query.MinLon is null || query.MinLat is null || query.MaxLon is null || query.MaxLat is null)
        {
            return null;
        }
        var envelope = new Envelope(query.MinLon.Value, query.MaxLon.Value, query.MinLat.Value, query.MaxLat.Value);
        var geometry = SpatialMath.Wgs84Factory.ToGeometry(envelope);
        geometry.SRID = SpatialMath.Wgs84;
        return geometry;
    }

    private static Dictionary<string, object?> Props(params (string Key, object? Value)[] values)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            dictionary[key] = value;
        }
        return dictionary;
    }

    private static bool IsDisplayAttribute(string name) => name is
        "name" or "name:en" or "waterway" or "natural" or "water" or "man_made" or "place" or "landuse"
        or "osm_type" or "osm_id" or "osm_url" or "waterwatch_geometry" or "intermittent" or "tidal"
        or "usage" or "basin" or "description" or "operator" or "ref";
}
