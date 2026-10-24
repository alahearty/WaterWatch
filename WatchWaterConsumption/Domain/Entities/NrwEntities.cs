using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class DmaWaterBalance : EntityBase
{
    public Guid DmaId { get; set; }
    public OperationalArea Dma { get; set; } = null!;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal InputVolumeM3 { get; set; }
    public decimal BilledVolumeM3 { get; set; }
    public decimal? UnbilledAuthorisedM3 { get; set; }
    public decimal? ApparentLossesM3 { get; set; }
    public decimal? RealLossesM3 { get; set; }
    public decimal? MinimumNightFlowM3PerHour { get; set; }
    public string? Method { get; set; }
    public string? UncertaintyNotes { get; set; }
    public string? Notes { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
}

public class LeakIncident : EntityBase
{
    public string Reference { get; set; } = string.Empty;
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public Point? Geometry { get; set; }
    public Guid? DmaId { get; set; }
    public OperationalArea? Dma { get; set; }
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Medium;
    public LeakStatus Status { get; set; } = LeakStatus.Reported;
    public decimal? EstimatedLossM3PerHour { get; set; }
    public bool EstimatedLossIsEstimate { get; set; } = true;
    public Guid? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Notes { get; set; }
}
