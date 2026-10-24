using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record AvailabilityDto(string Status, string Message);

public sealed record DataSourceDto(
    Guid Id,
    string Name,
    string Publisher,
    string? Licence,
    string? SourceUrl,
    DateTimeOffset? AcquiredAt,
    string? QualityNotes,
    bool IsOfficial);

public sealed record SourceRefDto(Guid? Id, string Name, string? Licence, string? SourceUrl);

public sealed record MapExtentDto(double MinLon, double MinLat, double MaxLon, double MaxLat);

public sealed class AssetListQuery
{
    public string? Search { get; set; }
    public AssetType? Type { get; set; }
    public AssetStatus? Status { get; set; }
    public Guid? AdminAreaId { get; set; }
    public Guid? OperationalAreaId { get; set; }
    public double? MinLon { get; set; }
    public double? MinLat { get; set; }
    public double? MaxLon { get; set; }
    public double? MaxLat { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public bool IncludeSensitive { get; set; }
}

public sealed record AssetSummaryDto(
    Guid Id,
    string AssetCode,
    string Name,
    AssetType AssetType,
    AssetStatus Status,
    ConditionGrade Condition,
    double? Longitude,
    double? Latitude,
    string? AdminAreaName,
    string? OperationalAreaName,
    bool IsSensitive,
    DataClassification DataClassification,
    string? SourceName);

public sealed record AssetDetailDto(
    Guid Id,
    string AssetCode,
    string Name,
    AssetType AssetType,
    string? Description,
    AssetStatus Status,
    ConditionGrade Condition,
    string? GeometryGeoJson,
    double? Longitude,
    double? Latitude,
    Guid? AdminAreaId,
    string? AdminAreaName,
    Guid? OperationalAreaId,
    string? OperationalAreaName,
    string? Owner,
    string? Operator,
    DateOnly? InstalledOn,
    decimal? CapacityM3PerDay,
    string? SpecificationsJson,
    string? DesignParametersJson,
    bool IsSensitive,
    DataClassification DataClassification,
    string? SourceReference,
    SourceRefDto? Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AssetDocumentDto> Documents,
    IReadOnlyList<InspectionDto> Inspections,
    IReadOnlyList<MaintenanceDto> MaintenanceRecords);

public sealed record AssetDocumentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string? Caption, DateTimeOffset UploadedAt);
public sealed record InspectionDto(Guid Id, DateTimeOffset ScheduledFor, DateTimeOffset? CompletedAt, string? InspectorName, string? Findings, ConditionGrade ConditionAfter);
public sealed record MaintenanceDto(Guid Id, WorkOrderType Type, DateTimeOffset PerformedAt, string? PerformedBy, string? Description, decimal? Cost);

public class AssetCreateRequest
{
    [Required, MaxLength(60)]
    public string AssetCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AssetType AssetType { get; set; }
    public string? Description { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? GeometryGeoJson { get; set; }
    public Guid? AdminAreaId { get; set; }
    public Guid? OperationalAreaId { get; set; }
    public string? Owner { get; set; }
    public string? Operator { get; set; }
    public DateOnly? InstalledOn { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Unknown;
    public ConditionGrade Condition { get; set; } = ConditionGrade.Unknown;
    public decimal? CapacityM3PerDay { get; set; }
    public string? SpecificationsJson { get; set; }
    public string? DesignParametersJson { get; set; }
    public bool IsSensitive { get; set; }
    public string? SourceReference { get; set; }
}

public sealed class AssetUpdateRequest : AssetCreateRequest
{
}

public sealed class InspectionCreateRequest
{
    public DateTimeOffset ScheduledFor { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? InspectorName { get; set; }
    public string? Findings { get; set; }
    public ConditionGrade ConditionAfter { get; set; } = ConditionGrade.Unknown;
}

public sealed class MaintenanceCreateRequest
{
    public WorkOrderType Type { get; set; } = WorkOrderType.Maintenance;
    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? PerformedBy { get; set; }
    public string? Description { get; set; }
    public decimal? Cost { get; set; }
    public Guid? WorkOrderId { get; set; }
}

public sealed record AssetStatisticsDto(int Total, IReadOnlyList<CountByLabel> ByType, IReadOnlyList<CountByLabel> ByStatus, IReadOnlyList<CountByLabel> ByCondition, int WithoutGeometry);

public sealed record CountByLabel(string Label, int Count, double? Value = null);
