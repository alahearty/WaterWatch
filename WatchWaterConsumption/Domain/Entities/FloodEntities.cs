using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class FloodEvent : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public Severity Severity { get; set; } = Severity.Medium;
    public string? Description { get; set; }
    public string? Source { get; set; }
    public bool IsOfficial { get; set; }
    public string? MetadataJson { get; set; }
}

public class FloodLayer : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public FloodLayerType LayerType { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public Geometry? Geometry { get; set; }
    public bool IsOfficial { get; set; }
    public string? Assumptions { get; set; }
    public string? Uncertainty { get; set; }
    public string? ValidationStatus { get; set; }
    public string? ModelVersion { get; set; }
    public Guid? FloodEventId { get; set; }
    public FloodEvent? FloodEvent { get; set; }
}

public class FloodExposureResult : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public Guid FloodLayerId { get; set; }
    public FloodLayer FloodLayer { get; set; } = null!;
    public DateTimeOffset AnalysedAt { get; set; } = DateTimeOffset.UtcNow;
    public string AnalysedBy { get; set; } = string.Empty;
    public int AssetsExposed { get; set; }
    public int SamplingStationsExposed { get; set; }
    public int DevicesExposed { get; set; }
    public long? PopulationExposed { get; set; }
    public string? Method { get; set; }
    public string? ParametersJson { get; set; }
    public string? ResultsJson { get; set; }
    public string? Limitations { get; set; }
}
