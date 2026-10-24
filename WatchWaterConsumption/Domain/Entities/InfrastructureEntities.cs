using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class Asset : EntityBase
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public string? Description { get; set; }
    public Geometry? Geometry { get; set; }
    public Guid? AdminAreaId { get; set; }
    public AdminArea? AdminArea { get; set; }
    public Guid? OperationalAreaId { get; set; }
    public OperationalArea? OperationalArea { get; set; }
    public string? Owner { get; set; }
    public string? Operator { get; set; }
    public DateOnly? InstalledOn { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Unknown;
    public ConditionGrade Condition { get; set; } = ConditionGrade.Unknown;
    public string? SpecificationsJson { get; set; }
    public decimal? CapacityM3PerDay { get; set; }
    public string? DesignParametersJson { get; set; }
    public bool IsSensitive { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string? SourceReference { get; set; }
    public ICollection<AssetDocument> Documents { get; set; } = new List<AssetDocument>();
    public ICollection<InspectionRecord> Inspections { get; set; } = new List<InspectionRecord>();
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
}

public class AssetDocument : EntityBase
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? UploadedBy { get; set; }
}

public class InspectionRecord : EntityBase
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public DateTimeOffset ScheduledFor { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? InspectorName { get; set; }
    public string? Findings { get; set; }
    public ConditionGrade ConditionAfter { get; set; } = ConditionGrade.Unknown;
    public string? PhotoPathsJson { get; set; }
}

public class MaintenanceRecord : EntityBase
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public WorkOrderType Type { get; set; } = WorkOrderType.Maintenance;
    public DateTimeOffset PerformedAt { get; set; }
    public string? PerformedBy { get; set; }
    public string? Description { get; set; }
    public decimal? Cost { get; set; }
    public Guid? WorkOrderId { get; set; }
}

public class NetworkNode : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public NetworkNodeType NodeType { get; set; }
    public Point? Geometry { get; set; }
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public bool IsVerified { get; set; }
    public string? Notes { get; set; }
}

public class NetworkEdge : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Guid FromNodeId { get; set; }
    public NetworkNode FromNode { get; set; } = null!;
    public Guid ToNodeId { get; set; }
    public NetworkNode ToNode { get; set; } = null!;
    public LineString? Geometry { get; set; }
    public decimal? DiameterMm { get; set; }
    public string? Material { get; set; }
    public bool IsVerified { get; set; }
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public decimal? LengthM { get; set; }
}
