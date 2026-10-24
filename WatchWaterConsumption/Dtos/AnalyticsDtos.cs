using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record PointInPolygonResultDto(
    double Longitude, double Latitude, AdminAreaDto? Lga, IReadOnlyList<OperationalAreaDto> OperationalAreas,
    int AssetsAtPoint, string Interpretation);

public sealed record BufferRequest
{
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? LayerKey { get; set; }
    public Guid? FeatureId { get; set; }
    public double RadiusMeters { get; set; } = 500;
}

public sealed record BufferAnalysisDto(
    string InputLabel, double RadiusMeters, double BufferAreaSqKm, string GeometryGeoJson,
    int AssetsInside, int SamplingStationsInside, int DevicesInside,
    IReadOnlyList<string> AssetNames, string Method);

public sealed record NearestAssetDto(
    Guid Id, string AssetCode, string Name, AssetType AssetType, AssetStatus Status,
    double DistanceMeters, double Longitude, double Latitude, bool IsSensitive);

public sealed record CoverageResultDto(
    AssetType? AssetType, double RadiusMeters, double CoverageAreaSqKm, double TotalLgaAreaSqKm,
    double CoveragePercent, int AssetsUsed, string GeometryGeoJson, string Method, string Limitations);

public sealed record DensityRowDto(string AreaName, string AreaType, int Count, double? AreaSqKm, double? PerSquareKm);

public sealed record NetworkNodeDto(Guid Id, string Code, string Name, NetworkNodeType NodeType, bool IsVerified, double? Longitude, double? Latitude);

public sealed record NetworkEdgeDto(Guid Id, string Code, Guid FromNodeId, Guid ToNodeId, bool IsVerified, decimal? DiameterMm, string? Material, double? LengthM);

public sealed record NetworkAssetDto(Guid Id, string AssetCode, string Name, AssetType AssetType, AssetStatus Status);

public sealed record NetworkTraceDto(
    Guid StartNodeId, string Direction, IReadOnlyList<NetworkNodeDto> Nodes, IReadOnlyList<NetworkEdgeDto> Edges,
    IReadOnlyList<NetworkAssetDto> AssetsAffected, string Method, string Limitations);

public sealed record IsolationAnalysisDto(
    Guid ClosedEdgeId, string ClosedEdgeCode, IReadOnlyList<NetworkNodeDto> IsolatedNodes,
    IReadOnlyList<NetworkEdgeDto> IsolatedEdges, string Method, string Limitations);
