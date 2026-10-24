using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class SamplingStation : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public WaterSourceType WaterSourceType { get; set; }
    public Point? Geometry { get; set; }
    public Guid? AdminAreaId { get; set; }
    public AdminArea? AdminArea { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public OperationalArea? ServiceZone { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public ICollection<WaterSample> Samples { get; set; } = new List<WaterSample>();
}

public class QualityStandard : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? ReferenceUrl { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public string? Notes { get; set; }
    public ICollection<QualityLimit> Limits { get; set; } = new List<QualityLimit>();
}

public class QualityLimit : EntityBase
{
    public Guid StandardId { get; set; }
    public QualityStandard Standard { get; set; } = null!;
    public ParameterCode Parameter { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? Notes { get; set; }
    public string? ReferenceSection { get; set; }
}

public class SamplingCampaign : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SamplingCampaignStatus Status { get; set; } = SamplingCampaignStatus.Planned;
    public DateOnly StartedOn { get; set; }
    public DateOnly? EndedOn { get; set; }
    public string? Purpose { get; set; }
    public string? LaboratoryName { get; set; }
    public bool LaboratoryAccredited { get; set; }
    public string? AccreditationReference { get; set; }
    public ICollection<WaterSample> Samples { get; set; } = new List<WaterSample>();
}

public class WaterSample : EntityBase
{
    public string SampleCode { get; set; } = string.Empty;
    public Guid StationId { get; set; }
    public SamplingStation Station { get; set; } = null!;
    public Guid CampaignId { get; set; }
    public SamplingCampaign Campaign { get; set; } = null!;
    public DateTimeOffset SampledAt { get; set; }
    public string? SampledBy { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public string? Notes { get; set; }
    public ICollection<SampleResult> Results { get; set; } = new List<SampleResult>();
}

public class SampleResult : EntityBase
{
    public Guid SampleId { get; set; }
    public WaterSample Sample { get; set; } = null!;
    public ParameterCode Parameter { get; set; }
    public decimal? Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? DetectionLimit { get; set; }
    public string? Method { get; set; }
    public string? Qualifier { get; set; }
    public QualityState State { get; set; } = QualityState.InsufficientEvidence;
    public Guid? AppliedLimitId { get; set; }
    public QualityLimit? AppliedLimit { get; set; }
    public string? StandardCode { get; set; }
    public string? AssessmentNote { get; set; }
    public DateTimeOffset? AssessedAt { get; set; }
}
