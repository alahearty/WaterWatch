using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record LayerDto(
    string Key,
    string Name,
    string Category,
    string GeometryType,
    int FeatureCount,
    bool IsPublic,
    bool IsVisibleByDefault,
    string? StyleJson,
    DataClassification DataClassification,
    string? SourceName,
    string? Licence,
    string? ServiceType = null,
    string? ServiceUrl = null,
    string? ServiceLayerName = null);

public sealed record AdminAreaDto(
    Guid Id,
    string Code,
    string Name,
    string? Alias,
    AdminLevel Level,
    Guid? ParentId,
    double? AreaSqKm,
    long? Population,
    int? PopulationYear,
    string? PopulationSource,
    DataClassification DataClassification);

public sealed record OperationalAreaDto(
    Guid Id,
    string Code,
    string Name,
    OperationalAreaType AreaType,
    double? AreaSqKm,
    long? Population,
    bool IsOfficial,
    DataClassification DataClassification,
    string? Notes);

public sealed record GeographyContextDto(
    string Country,
    string State,
    IReadOnlyList<AdminAreaDto> Lgas,
    MapExtentDto Extent,
    double InitialLatitude,
    double InitialLongitude,
    int InitialZoom,
    bool DemoMode);

public sealed record SearchResultDto(
    string Type,
    string Label,
    string? Subtitle,
    Guid? Id,
    double? Longitude,
    double? Latitude,
    string? LayerKey,
    string? Url);

public sealed class LayerFeatureQuery
{
    public double? MinLon { get; set; }
    public double? MinLat { get; set; }
    public double? MaxLon { get; set; }
    public double? MaxLat { get; set; }
    public Guid? AdminAreaId { get; set; }
    public DataClassification? Classification { get; set; }
    public AssetType? AssetType { get; set; }
    public AssetStatus? Status { get; set; }
    public int Limit { get; set; } = 5000;
}
