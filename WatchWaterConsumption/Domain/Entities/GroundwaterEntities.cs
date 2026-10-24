using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class BoreholeDetail : EntityBase
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
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

public class PumpingTest : EntityBase
{
    public Guid BoreholeAssetId { get; set; }
    public Asset Borehole { get; set; } = null!;
    public DateTimeOffset TestedAt { get; set; }
    public int DurationMinutes { get; set; }
    public decimal? PumpingRateM3PerHour { get; set; }
    public decimal? DrawdownM { get; set; }
    public int? RecoveryMinutes { get; set; }
    public string? ResultJson { get; set; }
    public string? ConductedBy { get; set; }
}

public class GroundwaterReading : EntityBase
{
    public Guid BoreholeAssetId { get; set; }
    public Asset Borehole { get; set; } = null!;
    public DateTimeOffset ObservedAt { get; set; }
    public decimal? WaterLevelM { get; set; }
    public decimal? AbstractionM3 { get; set; }
    public string? Notes { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
}
