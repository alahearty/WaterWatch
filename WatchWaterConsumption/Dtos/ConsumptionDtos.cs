using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record ConsumptionSummaryDto(
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    decimal TotalM3,
    decimal TotalLitres,
    decimal TotalKilolitres,
    int Accounts,
    int Meters,
    int Readings,
    decimal EstimatedSharePercent,
    int AnomalyCount,
    DateOnly? FirstReading,
    DateOnly? LastReading,
    string CoverageMessage,
    DataClassification DataClassification);

public sealed record ConsumptionAggregateDto(
    string Label,
    string? Code,
    decimal ConsumptionM3,
    decimal ConsumptionLitres,
    int Accounts,
    long? Population,
    decimal? LitresPerCapitaPerDay,
    DataClassification DataClassification);

public sealed record ConsumptionTrendDto(
    string Period,
    decimal ConsumptionM3,
    decimal ConsumptionLitres,
    int Readings,
    decimal EstimatedM3,
    DataClassification DataClassification);

public sealed record ConsumptionAnomalyDto(
    Guid MeterId,
    string MeterSerial,
    string? AccountNumber,
    string? Zone,
    DateOnly ReadingDate,
    decimal ConsumptionM3,
    decimal? PreviousConsumptionM3,
    string Reason,
    DataClassification DataClassification);

public sealed record ConsumerAccountDto(
    Guid Id, string AccountNumber, string Name, ConsumerCategory Category, string? Address,
    string? AdminArea, string? ServiceZone, string? Dma, int MeterCount, bool IsSensitive,
    DataClassification DataClassification);

public sealed record MeterDto(
    Guid Id, string SerialNumber, string? AccountNumber, string? ConsumerName, string Status,
    decimal Multiplier, string Unit, DateOnly? InstalledOn, DateOnly? LastReading, decimal? LastValueM3,
    DataClassification DataClassification);

public sealed record MeterReadingDto(
    Guid Id, Guid MeterId, string MeterSerial, string? AccountNumber, DateOnly ReadingDate,
    decimal ReadingValueM3, ReadingType ReadingType, string? CorrectionReason, string? Notes,
    DataClassification DataClassification);

public sealed record ReadingImportResultDto(
    Guid BatchId, string Status, int RowsTotal, int RowsAccepted, int RowsRejected,
    IReadOnlyList<ReadingImportError> Errors, DateOnly? PeriodStart, DateOnly? PeriodEnd);

public sealed record ReadingImportError(int Row, string Message);

public sealed class MeterReadingImportOptions
{
    public bool Commit { get; set; }
}

public sealed class ReadingCorrectionRequest
{
    [Required]
    public decimal ValueM3 { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ConsumptionQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Dimension { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}

public sealed class ConsumerAccountCreateRequest
{
    [Required, MaxLength(60)] public string AccountNumber { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public ConsumerCategory Category { get; set; } = ConsumerCategory.Domestic;
    public string? Address { get; set; }
    public Guid? AdminAreaId { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public Guid? DmaId { get; set; }
    public int? HouseholdSize { get; set; }
    public bool IsSensitive { get; set; } = true;
}

public sealed class MeterCreateRequest
{
    [Required, MaxLength(80)] public string SerialNumber { get; set; } = string.Empty;
    public Guid? ConsumerAccountId { get; set; }
    public Guid? AssetId { get; set; }
    public DateOnly? InstalledOn { get; set; }
    public MeterStatus Status { get; set; } = MeterStatus.Active;
    public decimal Multiplier { get; set; } = 1m;
    public string Unit { get; set; } = "m3";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public decimal? RolloverValueM3 { get; set; }
}

public sealed class MeterReadingQuery
{
    public Guid? MeterId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 200;
}
