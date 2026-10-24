using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class Device : EntityBase
{
    public string DeviceCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public Guid? HydrologyStationId { get; set; }
    public HydrologyStation? HydrologyStation { get; set; }
    public Point? Geometry { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Active;
    public DateTimeOffset? LastSeenAt { get; set; }
    public string? FirmwareVersion { get; set; }
    public DateOnly? InstalledOn { get; set; }
    public string? ApiKeyHash { get; set; }
    public int OfflineThresholdMinutes { get; set; } = 180;
    public bool IsDemoDevice { get; set; }
    public ICollection<TelemetryReading> Readings { get; set; } = new List<TelemetryReading>();
}

public class TelemetryReading : EntityBase
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public DateTimeOffset ObservedAt { get; set; }
    public string Parameter { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public TelemetryQuality Quality { get; set; } = TelemetryQuality.Observed;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RawPayloadJson { get; set; }
}

public class AlertRule : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public DeviceType? DeviceType { get; set; }
    public string Parameter { get; set; } = string.Empty;
    public AlertOperator Operator { get; set; }
    public decimal? Threshold { get; set; }
    public decimal? Threshold2 { get; set; }
    public Severity Severity { get; set; } = Severity.High;
    public bool IsActive { get; set; } = true;
    public int EscalationMinutes { get; set; } = 60;
    public string? Description { get; set; }
}

public class Alert : EntityBase
{
    public Guid AlertRuleId { get; set; }
    public AlertRule AlertRule { get; set; } = null!;
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal? Value { get; set; }
    public string? Message { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Open;
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public int EscalationCount { get; set; }
}
