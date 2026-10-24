using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record ImportResultDto(
    Guid BatchId, string Status, string Format, string TargetLayerKey,
    int RowsTotal, int RowsAccepted, int RowsRejected, int Duplicates,
    IReadOnlyList<ImportErrorDto> Errors, IReadOnlyDictionary<string, object?> Sample,
    bool Committed, string Message);

public sealed record ImportErrorDto(int Row, string Message);

public sealed class ImportOptions
{
    [Required, MaxLength(100)] public string LayerKey { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string LayerName { get; set; } = string.Empty;
    public string Category { get; set; } = "Imported";
    public string? Description { get; set; }
    public int DeclaredSrid { get; set; } = 4326;
    public string? SourceName { get; set; }
    public string? SourcePublisher { get; set; }
    public string? Licence { get; set; }
    public string? LicenceUrl { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset? AcquiredAt { get; set; }
    public bool Commit { get; set; }
    public bool IsPublic { get; set; } = true;
}

public sealed class ServiceLayerCreateRequest
{
    [Required, MaxLength(100)] public string LayerKey { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string LayerName { get; set; } = string.Empty;
    [Required] public string ServiceType { get; set; } = "WMS";
    [Required, Url] public string ServiceUrl { get; set; } = string.Empty;
    [Required] public string ServiceLayerName { get; set; } = string.Empty;
    public string Category { get; set; } = "External service";
    public string? Licence { get; set; }
    public bool IsPublic { get; set; } = true;
}

public sealed class RasterMetadataRequest
{
    [Required, MaxLength(200)] public string LayerName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LayerKey { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Srid { get; set; } = 4326;
    public double? MinLon { get; set; }
    public double? MinLat { get; set; }
    public double? MaxLon { get; set; }
    public double? MaxLat { get; set; }
    public double? ResolutionMeters { get; set; }
    public string? AcquisitionDate { get; set; }
    public string? SourceName { get; set; }
    public string? Licence { get; set; }
    public string? MetadataJson { get; set; }
}
