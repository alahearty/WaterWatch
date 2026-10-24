using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record DmaDto(
    Guid Id, string Code, string Name, double? AreaSqKm, bool IsOfficial,
    DataClassification DataClassification, int LeakIncidents, int Balances,
    decimal? LatestNrwPercent, DateOnly? LatestPeriodEnd);

public sealed record WaterBalanceDto(
    Guid Id, Guid DmaId, string DmaName, string DmaCode, DateOnly PeriodStart, DateOnly PeriodEnd,
    decimal InputVolumeM3, decimal BilledVolumeM3, decimal? UnbilledAuthorisedM3,
    decimal? ApparentLossesM3, decimal? RealLossesM3,
    decimal NonRevenueWaterM3, decimal NonRevenueWaterPercent,
    decimal UnallocatedLossM3, string LossSplitEvidence, decimal? MinimumNightFlowM3PerHour,
    string? Method, string? UncertaintyNotes, string? Notes, DataClassification DataClassification);

public sealed record NrwSummaryDto(
    int Dmas, int Balances, decimal InputM3, decimal BilledM3, decimal NrwM3, decimal NrwPercent,
    decimal? ApparentM3, decimal? RealM3, decimal UnallocatedM3, int OpenLeaks,
    decimal EstimatedLeakLossM3PerHour, string AvailabilityMessage);

public sealed record NrwTrendDto(string Period, decimal InputM3, decimal BilledM3, decimal NrwM3, decimal NrwPercent, DataClassification DataClassification);

public sealed record LeakIncidentDto(
    Guid Id, string Reference, DateTimeOffset ReportedAt, Guid? DmaId, string? DmaName,
    Guid? AssetId, string? AssetName, double? Longitude, double? Latitude, string Description,
    Severity Severity, LeakStatus Status, decimal? EstimatedLossM3PerHour, bool EstimatedLossIsEstimate,
    Guid? WorkOrderId, DataClassification DataClassification);

public sealed record LeakHotspotDto(
    Guid? DmaId, string DmaName, int Incidents, int OpenIncidents, decimal EstimatedLossM3PerHour,
    double? Longitude, double? Latitude, DataClassification DataClassification);

public sealed record PressureAnomalyDto(
    Guid AlertId, string DeviceName, string DeviceCode, decimal? Value, DateTimeOffset TriggeredAt,
    string? Message, AlertStatus Status, Severity Severity);

public sealed class WaterBalanceCreateRequest
{
    public Guid DmaId { get; set; }
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
    public DataClassification DataClassification { get; set; } = DataClassification.Observed;
}

public sealed class LeakIncidentCreateRequest
{
    [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
    public Guid? DmaId { get; set; }
    public Guid? AssetId { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public Severity Severity { get; set; } = Severity.Medium;
    public decimal? EstimatedLossM3PerHour { get; set; }
    public DataClassification DataClassification { get; set; } = DataClassification.Observed;
}

public sealed class LeakStatusUpdateRequest
{
    public LeakStatus Status { get; set; }
    public string? Notes { get; set; }
    public Guid? WorkOrderId { get; set; }
}
