using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record FloodLayerDto(
    Guid Id, string Name, FloodLayerType LayerType, string? Description,
    DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, bool IsOfficial,
    string? Assumptions, string? Uncertainty, string? ValidationStatus, string? ModelVersion,
    string? SourceName, DataClassification DataClassification, bool IsOperationalWarning,
    string WarningEligibilityNote, DateTimeOffset UpdatedAt);

public sealed record FloodEventDto(
    Guid Id, string Name, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, Severity Severity,
    string? Description, string? Source, bool IsOfficial, DataClassification DataClassification);

public sealed record FloodAssetExposureDto(
    Guid Id, string AssetCode, string Name, AssetType AssetType, AssetStatus Status,
    bool IsSensitive, double? Longitude, double? Latitude);

public sealed record FloodLayerExposureDto(string LayerName, int Count);

public sealed record FloodExposureDto(
    Guid Id, Guid FloodLayerId, string FloodLayerName, DateTimeOffset AnalysedAt, string AnalysedBy,
    int AssetsExposed, int SamplingStationsExposed, int DevicesExposed, long? PopulationExposed,
    IReadOnlyList<FloodAssetExposureDto> Assets, IReadOnlyList<FloodLayerExposureDto> OtherLayers,
    string Method, string Limitations, DataClassification DataClassification);

public sealed class FloodLayerCreateRequest
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public FloodLayerType LayerType { get; set; } = FloodLayerType.ModelledSusceptibility;
    public string? Description { get; set; }
    public string? GeometryGeoJson { get; set; }
    public Guid? FloodEventId { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public bool IsOfficial { get; set; }
    public string? Assumptions { get; set; }
    public string? Uncertainty { get; set; }
    public string? ValidationStatus { get; set; }
    public string? ModelVersion { get; set; }
    public string? SourceName { get; set; }
}

public sealed class FloodEventCreateRequest
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public Severity Severity { get; set; } = Severity.Medium;
    public string? Description { get; set; }
    public string? Source { get; set; }
    public bool IsOfficial { get; set; }
}
