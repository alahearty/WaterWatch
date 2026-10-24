using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using WaterWatch.Data;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Security;

namespace WaterWatch.Services;

public interface ISpatialAnalysisService
{
    Task<PointInPolygonResultDto> PointInPolygonAsync(double longitude, double latitude, CancellationToken cancellationToken = default);
    Task<BufferAnalysisDto> BufferAsync(BufferRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NearestAssetDto>> NearestAssetsAsync(double longitude, double latitude, int limit, CancellationToken cancellationToken = default);
    Task<CoverageResultDto> CoverageAsync(AssetType? assetType, double radiusMeters, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DensityRowDto>> DensityAsync(string dimension, CancellationToken cancellationToken = default);
    Task<NetworkTraceDto> TraceNetworkAsync(Guid nodeId, string direction, bool includeUnverified, CancellationToken cancellationToken = default);
    Task<IsolationAnalysisDto> IsolateEdgeAsync(Guid edgeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkNodeDto>> GetNetworkNodesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkEdgeDto>> GetNetworkEdgesAsync(CancellationToken cancellationToken = default);
}

public class SpatialAnalysisService : ISpatialAnalysisService
{
    private readonly DatabaseContext _db;
    private readonly IGeoJsonService _geoJson;
    private readonly IHttpContextAccessor _http;

    public SpatialAnalysisService(DatabaseContext db, IGeoJsonService geoJson, IHttpContextAccessor http)
    {
        _db = db;
        _geoJson = geoJson;
        _http = http;
    }

    private bool CanViewSensitive =>
        _http.HttpContext?.User is { Identity.IsAuthenticated: true } user &&
        Policies.Definitions.First(p => p.Name == Policies.CanViewSensitive).Roles.Any(user.IsInRole);

    public async Task<PointInPolygonResultDto> PointInPolygonAsync(double longitude, double latitude, CancellationToken cancellationToken = default)
    {
        if (longitude is < -180 or > 180 || latitude is < -90 or > 90)
        {
            throw new ArgumentException("Longitude/latitude are outside valid WGS84 bounds.");
        }
        var point = SpatialMath.Point(longitude, latitude);
        var lga = await _db.AdminAreas.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Level == AdminLevel.Lga && a.Geometry != null && a.Geometry.Contains(point), cancellationToken);
        var areas = await _db.OperationalAreas.AsNoTracking()
            .Where(a => a.Geometry != null && a.Geometry.Contains(point)).ToListAsync(cancellationToken);
        var assetCount = await _db.Assets.CountAsync(a => a.Geometry != null && a.Geometry.Distance(point) < 0.0001, cancellationToken);
        var interpretation = lga is null
            ? "Point is outside the loaded Port Harcourt / Obio-Akpor LGA boundaries."
            : $"Point is inside {lga.Name} LGA and {areas.Count} configured operational area(s).";
        return new PointInPolygonResultDto(longitude, latitude,
            lga is null ? null : new AdminAreaDto(lga.Id, lga.Code, lga.Name, lga.Alias, lga.Level,
                lga.ParentId, lga.AreaSqKm, lga.Population, lga.PopulationYear, lga.PopulationSource, lga.DataClassification),
            areas.Select(a => new OperationalAreaDto(a.Id, a.Code, a.Name, a.AreaType, a.AreaSqKm,
                a.Population, a.IsOfficial, a.DataClassification, a.Notes)).ToList(),
            assetCount, interpretation);
    }

    public async Task<BufferAnalysisDto> BufferAsync(BufferRequest request, CancellationToken cancellationToken = default)
    {
        var (geometry, label) = await ResolveGeometryAsync(request, cancellationToken);
        if (geometry is null)
        {
            throw new ArgumentException("Supply longitude/latitude or a valid layer and feature id.");
        }
        var radius = Math.Clamp(request.RadiusMeters, 1, 50000);
        var buffer = SpatialMath.BufferGeometryMeters(geometry, radius);
        buffer.SRID = SpatialMath.Wgs84;
        var assets = await _db.Assets.AsNoTracking()
            .Where(a => a.Geometry != null && a.Geometry.Intersects(buffer)).ToListAsync(cancellationToken);
        var stationCount = await _db.SamplingStations.AsNoTracking().CountAsync(s => s.Geometry != null && s.Geometry.Intersects(buffer), cancellationToken);
        var deviceCount = await _db.Devices.AsNoTracking().CountAsync(d => d.Geometry != null && d.Geometry.Intersects(buffer), cancellationToken);
        var visibleNames = assets.Where(a => !a.IsSensitive || CanViewSensitive).Select(a => a.Name).Take(30).ToList();
        return new BufferAnalysisDto(label, radius, Math.Round(SpatialMath.AreaSquareKilometers(buffer), 3),
            _geoJson.SerializeGeometry(buffer), assets.Count, stationCount, deviceCount, visibleNames,
            "Buffer generated in EPSG:32632 (UTM 32N) with a planar metric distance, then transformed back to EPSG:4326. Intersections use PostGIS.");
    }

    private async Task<(Geometry? Geometry, string Label)> ResolveGeometryAsync(BufferRequest request, CancellationToken cancellationToken)
    {
        if (request.Longitude is not null && request.Latitude is not null)
        {
            return (SpatialMath.Point(request.Longitude.Value, request.Latitude.Value),
                $"{request.Latitude:0.0000}, {request.Longitude:0.0000}");
        }
        if (request.FeatureId is null)
        {
            return (null, string.Empty);
        }
        var id = request.FeatureId.Value;
        return request.LayerKey switch
        {
            "assets" => await AssetGeometryAsync(id, cancellationToken),
            "sampling-stations" => await StationGeometryAsync(id, cancellationToken),
            "hydrology-stations" => await HydrologyGeometryAsync(id, cancellationToken),
            "devices" => await DeviceGeometryAsync(id, cancellationToken),
            "operational-zones" => await OperationalGeometryAsync(id, cancellationToken),
            "admin-lga" or "admin-state" => await AdminGeometryAsync(id, cancellationToken),
            "flood-layers" => await FloodGeometryAsync(id, cancellationToken),
            _ => await SpatialFeatureGeometryAsync(id, cancellationToken)
        };
    }

    private async Task<(Geometry?, string)> AssetGeometryAsync(Guid id, CancellationToken ct)
    {
        var asset = await _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        return (asset?.Geometry, asset is null ? "Asset" : $"Asset: {asset.Name}");
    }

    private async Task<(Geometry?, string)> StationGeometryAsync(Guid id, CancellationToken ct)
    {
        var station = await _db.SamplingStations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return (station?.Geometry, station is null ? "Sampling station" : $"Sampling station: {station.Name}");
    }

    private async Task<(Geometry?, string)> HydrologyGeometryAsync(Guid id, CancellationToken ct)
    {
        var station = await _db.HydrologyStations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return (station?.Geometry, station is null ? "Hydrology station" : $"Hydrology station: {station.Name}");
    }

    private async Task<(Geometry?, string)> DeviceGeometryAsync(Guid id, CancellationToken ct)
    {
        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return (device?.Geometry, device is null ? "Device" : $"Device: {device.Name}");
    }

    private async Task<(Geometry?, string)> OperationalGeometryAsync(Guid id, CancellationToken ct)
    {
        var area = await _db.OperationalAreas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        return (area?.Geometry, area is null ? "Operational area" : $"Operational area: {area.Name}");
    }

    private async Task<(Geometry?, string)> AdminGeometryAsync(Guid id, CancellationToken ct)
    {
        var area = await _db.AdminAreas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        return (area?.Geometry, area is null ? "Admin area" : $"Administrative area: {area.Name}");
    }

    private async Task<(Geometry?, string)> FloodGeometryAsync(Guid id, CancellationToken ct)
    {
        var layer = await _db.FloodLayers.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        return (layer?.Geometry, layer is null ? "Flood layer" : $"Flood layer: {layer.Name}");
    }

    private async Task<(Geometry?, string)> SpatialFeatureGeometryAsync(Guid id, CancellationToken ct)
    {
        var feature = await _db.SpatialFeatures.AsNoTracking().Include(f => f.Layer).FirstOrDefaultAsync(f => f.Id == id, ct);
        return (feature?.Geometry, feature is null ? "Spatial feature" : $"Spatial feature: {feature.Name ?? feature.Layer.Key}");
    }

    public async Task<IReadOnlyList<NearestAssetDto>> NearestAssetsAsync(double longitude, double latitude, int limit, CancellationToken cancellationToken = default)
    {
        if (longitude is < -180 or > 180 || latitude is < -90 or > 90)
        {
            throw new ArgumentException("Longitude/latitude are outside valid WGS84 bounds.");
        }
        limit = Math.Clamp(limit, 1, 50);
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a."Id", a."AssetCode", a."Name", a."AssetType", a."Status", a."IsSensitive",
                   ST_X(ST_Centroid(a."Geometry")) AS lon,
                   ST_Y(ST_Centroid(a."Geometry")) AS lat,
                   ST_Distance(ST_Transform(a."Geometry", 32632), ST_Transform(ST_SetSRID(ST_MakePoint(@lon, @lat), 4326), 32632)) AS distance_m
            FROM "Assets" a
            WHERE a."Geometry" IS NOT NULL AND (@includeSensitive OR NOT a."IsSensitive")
            ORDER BY distance_m
            LIMIT @limit
            """;
        var lonParam = command.CreateParameter();
        lonParam.ParameterName = "lon";
        lonParam.Value = longitude;
        command.Parameters.Add(lonParam);
        var latParam = command.CreateParameter();
        latParam.ParameterName = "lat";
        latParam.Value = latitude;
        command.Parameters.Add(latParam);
        var includeParam = command.CreateParameter();
        includeParam.ParameterName = "includeSensitive";
        includeParam.Value = CanViewSensitive;
        command.Parameters.Add(includeParam);
        var limitParam = command.CreateParameter();
        limitParam.ParameterName = "limit";
        limitParam.Value = limit;
        command.Parameters.Add(limitParam);

        var results = new List<NearestAssetDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new NearestAssetDto(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                Enum.Parse<AssetType>(reader.GetString(3)), Enum.Parse<AssetStatus>(reader.GetString(4)),
                Math.Round(reader.GetDouble(8), 1), reader.GetDouble(6), reader.GetDouble(7), reader.GetBoolean(5)));
        }
        return results;
    }

    public async Task<CoverageResultDto> CoverageAsync(AssetType? assetType, double radiusMeters, CancellationToken cancellationToken = default)
    {
        radiusMeters = Math.Clamp(radiusMeters, 50, 20000);
        var query = _db.Assets.AsNoTracking().Where(a => a.Geometry != null);
        if (assetType is not null)
        {
            query = query.Where(a => a.AssetType == assetType);
        }
        if (!CanViewSensitive)
        {
            query = query.Where(a => !a.IsSensitive);
        }
        var assets = await query.ToListAsync(cancellationToken);
        if (assets.Count == 0)
        {
            throw new InvalidOperationException("No assets with geometry are available for coverage analysis.");
        }
        var polygons = assets.Select(a => SpatialMath.ToUtm32(a.Geometry!).Buffer(radiusMeters)).ToList();
        var union = UnaryUnionOp.Union(polygons);
        var coverageWgs = SpatialMath.ToWgs84(union);
        coverageWgs.SRID = SpatialMath.Wgs84;
        var lgas = await _db.AdminAreas.AsNoTracking()
            .Where(a => a.Level == AdminLevel.Lga && a.Geometry != null).ToListAsync(cancellationToken);
        var totalArea = lgas.Sum(a => SpatialMath.AreaSquareKilometers(a.Geometry!));
        var coverageArea = SpatialMath.AreaSquareKilometers(union);
        return new CoverageResultDto(assetType, radiusMeters, Math.Round(coverageArea, 3),
            Math.Round(totalArea, 3), totalArea == 0 ? 0 : Math.Round(Math.Min(100, coverageArea / totalArea * 100), 1),
            assets.Count, _geoJson.SerializeGeometry(coverageWgs),
            "Metric buffers unioned in EPSG:32632 and transformed to EPSG:4326.",
            "Coverage is proximity to registered assets, not hydraulic serviceability or verified customer connections. Sensitive assets are excluded for users without permission.");
    }

    public async Task<IReadOnlyList<DensityRowDto>> DensityAsync(string dimension, CancellationToken cancellationToken = default)
    {
        var rows = new List<DensityRowDto>();
        if (dimension.Equals("servicezone", StringComparison.OrdinalIgnoreCase))
        {
            var zones = await _db.OperationalAreas.AsNoTracking()
                .Where(a => a.AreaType == OperationalAreaType.ServiceZone && a.Geometry != null).ToListAsync(cancellationToken);
            foreach (var zone in zones)
            {
                var count = await _db.Assets.CountAsync(a => a.Geometry != null && a.Geometry.Intersects(zone.Geometry!), cancellationToken);
                rows.Add(new DensityRowDto(zone.Name, "Service zone", count, zone.AreaSqKm,
                    zone.AreaSqKm is > 0 ? Math.Round(count / zone.AreaSqKm.Value, 2) : null));
            }
        }
        else
        {
            var lgas = await _db.AdminAreas.AsNoTracking()
                .Where(a => a.Level == AdminLevel.Lga && a.Geometry != null).ToListAsync(cancellationToken);
            foreach (var lga in lgas)
            {
                var count = await _db.Assets.CountAsync(a => a.Geometry != null && a.Geometry.Intersects(lga.Geometry!), cancellationToken);
                var area = lga.AreaSqKm ?? SpatialMath.AreaSquareKilometers(lga.Geometry!);
                rows.Add(new DensityRowDto(lga.Name, "LGA", count, Math.Round(area, 2),
                    area > 0 ? Math.Round(count / area, 2) : null));
            }
        }
        return rows.OrderByDescending(r => r.Count).ToList();
    }

    public async Task<NetworkTraceDto> TraceNetworkAsync(Guid nodeId, string direction, bool includeUnverified, CancellationToken cancellationToken = default)
    {
        var node = await _db.NetworkNodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken)
            ?? throw new KeyNotFoundException("Network node not found.");
        var edgesQuery = _db.NetworkEdges.AsNoTracking().AsQueryable();
        if (!includeUnverified)
        {
            edgesQuery = edgesQuery.Where(e => e.IsVerified);
        }
        var edges = await edgesQuery.ToListAsync(cancellationToken);
        var nodes = await _db.NetworkNodes.AsNoTracking()
            .Where(n => includeUnverified || n.IsVerified).ToDictionaryAsync(n => n.Id, cancellationToken);
        var downstream = !direction.Equals("upstream", StringComparison.OrdinalIgnoreCase);
        var visitedNodes = new HashSet<Guid> { nodeId };
        var visitedEdges = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(nodeId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var outgoing = downstream
                ? edges.Where(e => e.FromNodeId == current)
                : edges.Where(e => e.ToNodeId == current);
            foreach (var edge in outgoing)
            {
                if (!visitedEdges.Add(edge.Id))
                {
                    continue;
                }
                var next = downstream ? edge.ToNodeId : edge.FromNodeId;
                if (visitedNodes.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var assetIds = nodes.Where(n => visitedNodes.Contains(n.Key) && n.Value.AssetId != null)
            .Select(n => n.Value.AssetId!.Value)
            .Concat(edges.Where(e => visitedEdges.Contains(e.Id) && e.AssetId != null).Select(e => e.AssetId!.Value))
            .Distinct().ToList();
        var assets = await _db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id))
            .Select(a => new NetworkAssetDto(a.Id, a.AssetCode, a.Name, a.AssetType, a.Status)).ToListAsync(cancellationToken);
        return new NetworkTraceDto(nodeId, direction, visitedNodes.Where(nodes.ContainsKey).Select(id => ToNodeDto(nodes[id])).ToList(),
            edges.Where(e => visitedEdges.Contains(e.Id)).Select(ToEdgeDto).ToList(), assets,
            $"Breadth-first {(downstream ? "downstream" : "upstream")} traversal over {(includeUnverified ? "all" : "verified-only")} network edges.",
            "Tracing reflects recorded connectivity only. It does not model hydraulic capacity, pressure, valves found closed in the field, or unrecorded connections.");
    }

    public async Task<IsolationAnalysisDto> IsolateEdgeAsync(Guid edgeId, CancellationToken cancellationToken = default)
    {
        var edge = await _db.NetworkEdges.AsNoTracking().FirstOrDefaultAsync(e => e.Id == edgeId, cancellationToken)
            ?? throw new KeyNotFoundException("Network edge not found.");
        var edges = await _db.NetworkEdges.AsNoTracking().Where(e => e.IsVerified).ToListAsync(cancellationToken);
        var nodes = await _db.NetworkNodes.AsNoTracking().ToListAsync(cancellationToken);
        var remaining = edges.Where(e => e.Id != edgeId).ToList();
        var sources = nodes.Where(n => n.NodeType == NetworkNodeType.Source && n.IsVerified).Select(n => n.Id).ToList();
        var reachable = new HashSet<Guid>(sources);
        var queue = new Queue<Guid>(sources);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in remaining.Where(e => e.FromNodeId == current).Select(e => e.ToNodeId))
            {
                if (reachable.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }
        var isolatedNodeIds = nodes.Where(n => n.IsVerified && !reachable.Contains(n.Id)).Select(n => n.Id).ToHashSet();
        return new IsolationAnalysisDto(edge.Id, edge.Code,
            nodes.Where(n => isolatedNodeIds.Contains(n.Id)).Select(ToNodeDto).ToList(),
            remaining.Where(e => isolatedNodeIds.Contains(e.FromNodeId) && isolatedNodeIds.Contains(e.ToNodeId)).Select(ToEdgeDto).ToList(),
            "Closed one verified edge, then performed downstream reachability from all verified source nodes.",
            "Requires verified connectivity and at least one verified source node. Unknown valves/connections can invalidate the result.");
    }

    public async Task<IReadOnlyList<NetworkNodeDto>> GetNetworkNodesAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _db.NetworkNodes.AsNoTracking().OrderBy(n => n.Code).ToListAsync(cancellationToken);
        return nodes.Select(ToNodeDto).ToList();
    }

    public async Task<IReadOnlyList<NetworkEdgeDto>> GetNetworkEdgesAsync(CancellationToken cancellationToken = default)
    {
        var edges = await _db.NetworkEdges.AsNoTracking().OrderBy(e => e.Code).ToListAsync(cancellationToken);
        return edges.Select(ToEdgeDto).ToList();
    }

    private static NetworkNodeDto ToNodeDto(Domain.Entities.NetworkNode n) => new(n.Id, n.Code, n.Name, n.NodeType, n.IsVerified, n.Geometry?.X, n.Geometry?.Y);
    private static NetworkEdgeDto ToEdgeDto(Domain.Entities.NetworkEdge e) => new(e.Id, e.Code, e.FromNodeId, e.ToNodeId, e.IsVerified, e.DiameterMm, e.Material, (double?)e.LengthM);
}
