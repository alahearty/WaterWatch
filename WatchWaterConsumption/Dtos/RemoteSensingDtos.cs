using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record SatelliteSceneDto(
    string Provider, string Collection, string SceneId, DateTimeOffset AcquiredAt, decimal? CloudCover,
    decimal? ResolutionM, string? ThumbnailUrl, string? VisualUrl, string? SourceUrl,
    IReadOnlyList<double> BoundingBox, string Licence, DataClassification DataClassification);

public sealed record StacSearchResultDto(
    string Status, string? Message, string Collection, DateTimeOffset RetrievedAt,
    IReadOnlyList<SatelliteSceneDto> Scenes, string Source);

public sealed class StacSearchRequest
{
    public string Collection { get; set; } = "sentinel-2-l2a";
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(0, 100)] public double MaxCloudPercent { get; set; } = 30;
    [Range(1, 50)] public int Limit { get; set; } = 12;
}

public sealed record ChangeClassStatDto(string ClassName, double AreaSqKm, int FeatureCount, decimal? PercentOfLayer);

public sealed record ChangeLayerDto(
    string LayerKey, string Name, string? Description, string? PeriodFrom, string? PeriodTo,
    string? ChangeType, string SourceName, string? Licence, double TotalAreaSqKm,
    IReadOnlyList<ChangeClassStatDto> Classes, DateTimeOffset UpdatedAt, DataClassification DataClassification);
