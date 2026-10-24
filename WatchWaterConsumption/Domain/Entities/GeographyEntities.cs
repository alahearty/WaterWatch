using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DataClassification DataClassification { get; set; } = DataClassification.Observed;
}

public class DataSource : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string? Licence { get; set; }
    public string? LicenceUrl { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset? AcquiredAt { get; set; }
    public string? Coverage { get; set; }
    public string? QualityNotes { get; set; }
    public string? UpdateFrequency { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsOfficial { get; set; }
}

public class AdminArea : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public AdminLevel Level { get; set; }
    public Guid? ParentId { get; set; }
    public AdminArea? Parent { get; set; }
    public Geometry? Geometry { get; set; }
    public double? AreaSqKm { get; set; }
    public long? Population { get; set; }
    public int? PopulationYear { get; set; }
    public string? PopulationSource { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
}

public class OperationalArea : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public OperationalAreaType AreaType { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public OperationalArea? Parent { get; set; }
    public Geometry? Geometry { get; set; }
    public double? AreaSqKm { get; set; }
    public long? Population { get; set; }
    public bool IsOfficial { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string? Notes { get; set; }
}

public class SpatialLayer : EntityBase
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string GeometryType { get; set; } = "Geometry";
    public string? StyleJson { get; set; }
    public string? ServiceType { get; set; }
    public string? ServiceUrl { get; set; }
    public string? ServiceLayerName { get; set; }
    public int FeatureCount { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool IsVisibleByDefault { get; set; }
    public int MinZoom { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
}

public class SpatialFeature : EntityBase
{
    public Guid LayerId { get; set; }
    public SpatialLayer Layer { get; set; } = null!;
    public string? ExternalId { get; set; }
    public string? Name { get; set; }
    public Geometry Geometry { get; set; } = null!;
    public string? AttributesJson { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? GeometryHash { get; set; }
}

public class ImportBatch : EntityBase
{
    public string FileName { get; set; } = string.Empty;
    public string? TargetLayerKey { get; set; }
    public string? Format { get; set; }
    public string? RequestedBy { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Preview;
    public int RowsTotal { get; set; }
    public int RowsAccepted { get; set; }
    public int RowsRejected { get; set; }
    public string? ErrorsJson { get; set; }
    public string? ParametersJson { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
}
