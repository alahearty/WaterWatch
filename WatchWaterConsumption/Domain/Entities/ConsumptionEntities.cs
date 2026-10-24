using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class ConsumerAccount : EntityBase
{
    public string AccountNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ConsumerCategory Category { get; set; }
    public string? Address { get; set; }
    public Guid? AdminAreaId { get; set; }
    public AdminArea? AdminArea { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public OperationalArea? ServiceZone { get; set; }
    public Guid? DmaId { get; set; }
    public OperationalArea? Dma { get; set; }
    public int? HouseholdSize { get; set; }
    public bool IsSensitive { get; set; } = true;
    public string? Notes { get; set; }
    public ICollection<Meter> Meters { get; set; } = new List<Meter>();
}

public class Meter : EntityBase
{
    public string SerialNumber { get; set; } = string.Empty;
    public Guid? ConsumerAccountId { get; set; }
    public ConsumerAccount? ConsumerAccount { get; set; }
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public DateOnly? InstalledOn { get; set; }
    public MeterStatus Status { get; set; } = MeterStatus.Active;
    public decimal Multiplier { get; set; } = 1m;
    public string Unit { get; set; } = "m3";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public decimal? RolloverValueM3 { get; set; }
    public ICollection<MeterReading> Readings { get; set; } = new List<MeterReading>();
}

public class MeterReading : EntityBase
{
    public Guid MeterId { get; set; }
    public Meter Meter { get; set; } = null!;
    public DateOnly ReadingDate { get; set; }
    public decimal ReadingValueM3 { get; set; }
    public ReadingType ReadingType { get; set; } = ReadingType.Observed;
    public decimal? CorrectedFromValueM3 { get; set; }
    public string? CorrectionReason { get; set; }
    public Guid? ImportBatchId { get; set; }
    public ConsumptionImport? ImportBatch { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
    public string? ValidatedBy { get; set; }
}

public class ConsumptionImport : EntityBase
{
    public string FileName { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public ImportStatus Status { get; set; } = ImportStatus.Preview;
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public int RowsTotal { get; set; }
    public int RowsAccepted { get; set; }
    public int RowsRejected { get; set; }
    public string? ErrorsJson { get; set; }
    public string? Notes { get; set; }
}
