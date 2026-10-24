using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record DeviceDto(
    Guid Id, string DeviceCode, string Name, DeviceType DeviceType, DeviceStatus Status,
    Guid? AssetId, string? AssetName, double? Longitude, double? Latitude,
    DateTimeOffset? LastSeenAt, string? LastParameter, decimal? LastValue, string? LastUnit,
    bool IsOffline, int OfflineThresholdMinutes, bool IsDemoDevice, DataClassification DataClassification,
    string Freshness);

public sealed record DeviceCreateRequest
{
    [Required, MaxLength(80)] public string DeviceCode { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? HydrologyStationId { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? FirmwareVersion { get; set; }
    public DateOnly? InstalledOn { get; set; }
    public int OfflineThresholdMinutes { get; set; } = 180;
    public bool IsDemoDevice { get; set; }
}

public sealed record DeviceCreatedDto(DeviceDto Device, string ApiKey, string ApiKeyNote);

public sealed record TelemetryReadingDto(
    Guid Id, Guid DeviceId, string DeviceCode, string DeviceName, DateTimeOffset ObservedAt,
    string Parameter, decimal Value, string Unit, TelemetryQuality Quality,
    DataClassification DataClassification, bool IsSynthetic, DateTimeOffset ReceivedAt);

public sealed class TelemetryIngestRequest
{
    [Required, MaxLength(80)] public string DeviceCode { get; set; } = string.Empty;
    public DateTimeOffset? ObservedAt { get; set; }
    [Required, MaxLength(60)] public string Parameter { get; set; } = string.Empty;
    public decimal Value { get; set; }
    [Required, MaxLength(30)] public string Unit { get; set; } = string.Empty;
    public TelemetryQuality Quality { get; set; } = TelemetryQuality.Observed;
    public string? RawPayloadJson { get; set; }
}

public sealed record AlertRuleDto(
    Guid Id, string Name, DeviceType? DeviceType, string Parameter, AlertOperator Operator,
    decimal? Threshold, decimal? Threshold2, Severity Severity, bool IsActive, int EscalationMinutes,
    string? Description);

public sealed class AlertRuleCreateRequest
{
    [Required, MaxLength(160)] public string Name { get; set; } = string.Empty;
    public DeviceType? DeviceType { get; set; }
    [Required, MaxLength(60)] public string Parameter { get; set; } = string.Empty;
    public AlertOperator Operator { get; set; }
    public decimal? Threshold { get; set; }
    public decimal? Threshold2 { get; set; }
    public Severity Severity { get; set; } = Severity.High;
    public int EscalationMinutes { get; set; } = 60;
    public string? Description { get; set; }
}

public sealed record AlertDto(
    Guid Id, Guid AlertRuleId, string RuleName, Guid DeviceId, string DeviceName, string DeviceCode,
    DateTimeOffset TriggeredAt, decimal? Value, string? Message, AlertStatus Status, Severity Severity,
    string? AcknowledgedBy, DateTimeOffset? AcknowledgedAt, DateTimeOffset? ResolvedAt,
    bool EscalationDue, DateTimeOffset? EscalatedAt, int EscalationCount, DataClassification DataClassification);

public sealed class AlertAcknowledgeRequest
{
    public string? Notes { get; set; }
}

public sealed record TelemetrySummaryDto(
    int Devices, int ActiveDevices, int OfflineDevices, int DemoDevices, int Readings,
    int OpenAlerts, DateTimeOffset? LastReadingAt, string AvailabilityMessage, bool DemoMode);

public sealed class TelemetryQuery
{
    public Guid? DeviceId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Limit { get; set; } = 2000;
}
