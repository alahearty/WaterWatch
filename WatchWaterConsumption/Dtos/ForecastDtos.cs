using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record ForecastMetadataDto(
    string Status, string? Message, string ModelName, string ModelVersion,
    DateOnly? TrainingStart, DateOnly? TrainingEnd, int ObservationCount,
    string ValidationMethod, IReadOnlyDictionary<string, double?> Metrics,
    string? ConfidenceNote, string Limitations, DateTimeOffset GeneratedAt,
    DataClassification DataClassification);

public sealed record ForecastPointDto(string Period, double Value, double? Lower, double? Upper, string Unit, bool IsForecast);

public sealed record DemandForecastDto(ForecastMetadataDto Metadata, IReadOnlyList<ForecastPointDto> Points, string Unit);

public sealed record WaterLevelForecastDto(ForecastMetadataDto Metadata, Guid StationId, string StationName, IReadOnlyList<ForecastPointDto> Points, string Unit);

public sealed record AssetRiskDto(
    Guid AssetId, string AssetCode, string Name, AssetType AssetType, AssetStatus Status,
    ConditionGrade Condition, int RiskScore, string RiskBand, IReadOnlyList<string> Factors,
    double? AgeYears, bool HasOpenWorkOrder, int LeakIncidentsNearby, DataClassification DataClassification);

public sealed record LeakagePriorityDto(
    Guid? DmaId, string DmaName, decimal? NrwPercent, int OpenLeaks, decimal EstimatedLossM3PerHour,
    int RiskScore, string Priority, IReadOnlyList<string> Factors, DataClassification DataClassification);

public sealed record QualityDeteriorationDto(
    Guid StationId, string StationName, ParameterCode Parameter, int Observations,
    double SlopePerMonth, string Direction, string Interpretation, DataClassification DataClassification);

public sealed record ForecastOverviewDto(
    DemandForecastDto Demand, IReadOnlyList<AssetRiskDto> AssetRisks,
    IReadOnlyList<LeakagePriorityDto> LeakagePriorities, IReadOnlyList<QualityDeteriorationDto> QualityTrends,
    string AvailabilityMessage);
