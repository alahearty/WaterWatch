using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record HydrologyStationDto(
    Guid Id, string Code, string Name, HydrologyStationType StationType,
    double? Longitude, double? Latitude, string? WaterwayName, string? Datum,
    bool IsActive, DataClassification DataClassification, DateTimeOffset? LastObservedAt, int ObservationCount);

public sealed record HydrologyObservationDto(
    Guid Id, Guid StationId, string StationName, DateTimeOffset ObservedAt,
    HydrologyParameter Parameter, decimal Value, string Unit, QualityFlag QualityFlag,
    DataClassification DataClassification, string? SourceName, string? Notes);

public sealed record HydrologySummaryDto(
    int Stations, int RainfallStations, int WaterLevelStations, int Observations,
    int WaterwayFeatures, int WaterBodyFeatures, DateTimeOffset? LastObservationAt,
    string AvailabilityMessage);

public sealed record RainfallPointDto(string Date, decimal RainfallMm, string Source, string Classification);

public sealed record RainfallLiveDto(
    double Latitude, double Longitude, string Timezone, DateTimeOffset RetrievedAt,
    IReadOnlyList<RainfallPointDto> Daily, string Source, string Licence, string? CurrentTemperatureC,
    string? CurrentPrecipitationMm, string Status, string? Message);

public sealed record DrainageObstructionDto(
    Guid Id, string Reference, DateTimeOffset ReportedAt, double? Longitude, double? Latitude,
    string? WaterwayName, string Description, Severity Severity, string Status, string? ReportedBy,
    string? AdminArea, DataClassification DataClassification);

public sealed class HydrologyStationCreateRequest
{
    [Required, MaxLength(60)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public HydrologyStationType StationType { get; set; } = HydrologyStationType.Rainfall;
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? WaterwayName { get; set; }
    public Guid? AdminAreaId { get; set; }
    public string? Datum { get; set; }
    public string? Notes { get; set; }
}

public sealed class HydrologyObservationCreateRequest
{
    public Guid StationId { get; set; }
    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;
    public HydrologyParameter Parameter { get; set; }
    public decimal Value { get; set; }
    [Required, MaxLength(30)] public string Unit { get; set; } = string.Empty;
    public QualityFlag QualityFlag { get; set; } = QualityFlag.Observed;
    public string? Notes { get; set; }
}

public sealed class DrainageObstructionCreateRequest
{
    [Required, MaxLength(200)] public string WaterwayName { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public Severity Severity { get; set; } = Severity.Medium;
    public Guid? AdminAreaId { get; set; }
}

public sealed class HydrologyQuery
{
    public Guid? StationId { get; set; }
    public HydrologyParameter? Parameter { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Limit { get; set; } = 2000;
}
