using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class SatelliteScene : EntityBase
{
    public string Provider { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAt { get; set; }
    public decimal? CloudCover { get; set; }
    public decimal? ResolutionM { get; set; }
    public Geometry? Footprint { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? AssetsJson { get; set; }
    public string? Licence { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset RetrievedAt { get; set; } = DateTimeOffset.UtcNow;
}
