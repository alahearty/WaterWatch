using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class WorkOrder : EntityBase
{
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkOrderType Type { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Reported;
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public Point? Geometry { get; set; }
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? VerificationNotes { get; set; }
    public string? ResolvedBy { get; set; }
    public string? VerifiedBy { get; set; }
    public string? ClosedBy { get; set; }
    public string? PhotoPathsJson { get; set; }
    public ICollection<WorkOrderEvent> Events { get; set; } = new List<WorkOrderEvent>();
}

public class WorkOrderEvent : EntityBase
{
    public Guid WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public WorkOrderStatus? FromStatus { get; set; }
    public WorkOrderStatus ToStatus { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}

public class ReportJob : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public ReportFormat Format { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string? ParametersJson { get; set; }
    public string Status { get; set; } = "Completed";
    public string? ResultPath { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int RecordCount { get; set; }
}

public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}
