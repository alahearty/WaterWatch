using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record WorkOrderDto(
    Guid Id, string Reference, string Title, string? Description, WorkOrderType Type, Priority Priority,
    WorkOrderStatus Status, Guid? AssetId, string? AssetName, double? Longitude, double? Latitude,
    string? ReportedBy, string? AssignedTo, DateTimeOffset? DueAt, DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt, DateTimeOffset? VerifiedAt, DateTimeOffset? ClosedAt,
    string? ResolutionNotes, string? VerificationNotes, string? ResolvedBy, string? VerifiedBy,
    string? ClosedBy, DataClassification DataClassification, bool IsOverdue);

public sealed record WorkOrderEventDto(
    Guid Id, WorkOrderStatus? FromStatus, WorkOrderStatus ToStatus, string ChangedBy,
    DateTimeOffset ChangedAt, string? Notes);

public sealed record WorkOrderDetailDto(WorkOrderDto WorkOrder, IReadOnlyList<WorkOrderEventDto> Events);

public sealed record WorkOrderStatsDto(
    int Total, int Open, int Overdue, IReadOnlyList<CountByLabel> ByStatus,
    IReadOnlyList<CountByLabel> ByPriority, IReadOnlyList<CountByLabel> ByType, string AvailabilityMessage);

public sealed class WorkOrderCreateRequest
{
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkOrderType Type { get; set; } = WorkOrderType.Defect;
    public Priority Priority { get; set; } = Priority.Medium;
    public Guid? AssetId { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? AssignedTo { get; set; }
    public DateTimeOffset? DueAt { get; set; }
}

public sealed class WorkOrderTransitionRequest
{
    public WorkOrderStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
    public DateTimeOffset? DueAt { get; set; }
}

public sealed class WorkOrderQuery
{
    public WorkOrderStatus? Status { get; set; }
    public Priority? Priority { get; set; }
    public WorkOrderType? Type { get; set; }
    public string? AssignedTo { get; set; }
    public Guid? AssetId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
