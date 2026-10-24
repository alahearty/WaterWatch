using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record BoreholeDto(
    Guid AssetId, string AssetCode, string Name, AssetStatus Status, ConditionGrade Condition,
    decimal? DepthM, string? AquiferName, bool AquiferClassificationInferred, decimal? StaticWaterLevelM,
    decimal? DynamicWaterLevelM, decimal? YieldM3PerHour, DateTimeOffset? LastReadingAt, decimal? LastWaterLevelM,
    double? Longitude, double? Latitude, DataClassification DataClassification);

public sealed record BoreholeDetailDto(
    Guid AssetId, string AssetCode, string Name, AssetStatus Status, ConditionGrade Condition,
    decimal? BoreholeDepthM, decimal? ScreenTopM, decimal? ScreenBottomM,
    string? AquiferName, string? AquiferClassification, bool AquiferClassificationInferred,
    decimal? StaticWaterLevelM, decimal? DynamicWaterLevelM, decimal? YieldM3PerHour,
    DateOnly? DrilledOn, string? Driller, string? ConstructionJson, string? LithologyJson,
    DataClassification DataClassification);

public sealed record GroundwaterReadingDto(
    Guid Id, Guid BoreholeAssetId, DateTimeOffset ObservedAt, decimal? WaterLevelM, decimal? AbstractionM3,
    string? Notes, string? SourceName, DataClassification DataClassification);

public sealed record PumpingTestDto(
    Guid Id, Guid BoreholeAssetId, DateTimeOffset TestedAt, int DurationMinutes,
    decimal? PumpingRateM3PerHour, decimal? DrawdownM, int? RecoveryMinutes, string? ConductedBy,
    string? ResultJson, DataClassification DataClassification);

public sealed record GroundwaterTrendDto(string Period, decimal? AverageWaterLevelM, decimal? AbstractionM3, int Readings);

public sealed record ContaminationProximityDto(
    Guid StationId, string StationName, double DistanceM, string? LatestState, int NonCompliantParameters,
    IReadOnlyList<string> Parameters, DataClassification DataClassification);

public sealed record GroundwaterSummaryDto(
    int Boreholes, int WithDetails, int Readings, int PumpingTests, int AquiferInferred,
    DateTimeOffset? LastReadingAt, string AvailabilityMessage);

public sealed class BoreholeDetailUpsertRequest
{
    public decimal? BoreholeDepthM { get; set; }
    public decimal? ScreenTopM { get; set; }
    public decimal? ScreenBottomM { get; set; }
    public string? AquiferName { get; set; }
    public string? AquiferClassification { get; set; }
    public bool AquiferClassificationInferred { get; set; } = true;
    public decimal? StaticWaterLevelM { get; set; }
    public decimal? DynamicWaterLevelM { get; set; }
    public decimal? YieldM3PerHour { get; set; }
    public DateOnly? DrilledOn { get; set; }
    public string? Driller { get; set; }
    public string? ConstructionJson { get; set; }
    public string? LithologyJson { get; set; }
}

public sealed class GroundwaterReadingCreateRequest
{
    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal? WaterLevelM { get; set; }
    public decimal? AbstractionM3 { get; set; }
    public string? Notes { get; set; }
    public DataClassification DataClassification { get; set; } = DataClassification.Observed;
}

public sealed class PumpingTestCreateRequest
{
    public DateTimeOffset TestedAt { get; set; } = DateTimeOffset.UtcNow;
    [Range(1, 100000)] public int DurationMinutes { get; set; }
    public decimal? PumpingRateM3PerHour { get; set; }
    public decimal? DrawdownM { get; set; }
    public int? RecoveryMinutes { get; set; }
    public string? ConductedBy { get; set; }
    public string? ResultJson { get; set; }
    public DataClassification DataClassification { get; set; } = DataClassification.Observed;
}
