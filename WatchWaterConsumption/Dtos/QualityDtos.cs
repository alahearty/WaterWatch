using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record QualityLimitDto(ParameterCode Parameter, string Unit, decimal? MinValue, decimal? MaxValue, string? Notes, string? ReferenceSection);

public sealed record QualityStandardDto(string Code, string Name, string Authority, string Version, string? ReferenceUrl, DateOnly? EffectiveFrom, IReadOnlyList<QualityLimitDto> Limits);

public sealed record SamplingStationDto(
    Guid Id, string Code, string Name, WaterSourceType WaterSourceType, double? Longitude, double? Latitude,
    string? AdminArea, string? ServiceZone, bool IsActive, DataClassification DataClassification,
    DateTimeOffset? LastSampledAt, int SampleCount);

public sealed record SamplingCampaignDto(
    Guid Id, string Code, string Name, SamplingCampaignStatus Status, DateOnly StartedOn, DateOnly? EndedOn,
    string? Purpose, string? LaboratoryName, bool LaboratoryAccredited, int SampleCount);

public sealed record SampleResultDto(
    Guid Id, ParameterCode Parameter, decimal? Value, string Unit, decimal? DetectionLimit,
    QualityState State, string? StandardCode, string? AssessmentNote, string? Method,
    decimal? LimitMin, decimal? LimitMax, string? LimitUnit);

public sealed record WaterSampleDto(
    Guid Id, string SampleCode, Guid StationId, string StationName, string CampaignCode,
    DateTimeOffset SampledAt, string? SampledBy, string? Notes, DataClassification DataClassification,
    IReadOnlyList<SampleResultDto> Results);

public sealed record QualitySummaryDto(
    int Stations, int Campaigns, int Samples, int Results,
    int Compliant, int NonCompliant, int InsufficientEvidence, int NotAssessed,
    DateTimeOffset? LastSampleAt, string AvailabilityMessage);

public sealed record QualityExceedanceDto(
    Guid ResultId, string SampleCode, string StationName, ParameterCode Parameter, decimal? Value,
    string Unit, decimal? LimitMin, decimal? LimitMax, DateTimeOffset SampledAt, string? AssessmentNote,
    DataClassification DataClassification);

public sealed record QualityHotspotDto(
    Guid StationId, string StationName, ParameterCode Parameter, double? Longitude, double? Latitude,
    int NonCompliantResults, int TotalResults, string LatestState, DataClassification DataClassification);

public sealed record ResultTrendDto(string Period, decimal? Value, decimal? LimitMax, string Unit, QualityState State);

public sealed class SamplingStationCreateRequest
{
    [Required, MaxLength(60)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public WaterSourceType WaterSourceType { get; set; } = WaterSourceType.Groundwater;
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public Guid? AdminAreaId { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CampaignCreateRequest
{
    [Required, MaxLength(60)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public SamplingCampaignStatus Status { get; set; } = SamplingCampaignStatus.Active;
    public DateOnly StartedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? EndedOn { get; set; }
    public string? Purpose { get; set; }
    public string? LaboratoryName { get; set; }
    public bool LaboratoryAccredited { get; set; }
    public string? AccreditationReference { get; set; }
}

public sealed class SampleResultEntry
{
    public ParameterCode Parameter { get; set; }
    public decimal? Value { get; set; }
    [Required, MaxLength(30)] public string Unit { get; set; } = string.Empty;
    public decimal? DetectionLimit { get; set; }
    public string? Method { get; set; }
    public string? Qualifier { get; set; }
    public string? StandardCode { get; set; }
    public string? Note { get; set; }
}

public sealed class SampleCreateRequest
{
    [Required, MaxLength(80)] public string SampleCode { get; set; } = string.Empty;
    public Guid StationId { get; set; }
    public Guid CampaignId { get; set; }
    public DateTimeOffset SampledAt { get; set; } = DateTimeOffset.UtcNow;
    public string? SampledBy { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public string? Notes { get; set; }
    public DataClassification DataClassification { get; set; } = DataClassification.Observed;
    public List<SampleResultEntry> Results { get; set; } = new();
}

public sealed class QualityQuery
{
    public Guid? StationId { get; set; }
    public Guid? CampaignId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}
