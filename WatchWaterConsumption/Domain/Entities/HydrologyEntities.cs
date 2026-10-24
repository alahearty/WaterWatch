using NetTopologySuite.Geometries;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Domain.Entities;

public class HydrologyStation : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HydrologyStationType StationType { get; set; }
    public Point? Geometry { get; set; }
    public string? WaterwayName { get; set; }
    public Guid? AdminAreaId { get; set; }
    public AdminArea? AdminArea { get; set; }
    public string? Datum { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string? Notes { get; set; }
    public ICollection<HydrologyObservation> Observations { get; set; } = new List<HydrologyObservation>();
}

public class HydrologyObservation : EntityBase
{
    public Guid StationId { get; set; }
    public HydrologyStation Station { get; set; } = null!;
    public DateTimeOffset ObservedAt { get; set; }
    public HydrologyParameter Parameter { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public QualityFlag QualityFlag { get; set; } = QualityFlag.Observed;
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string? Notes { get; set; }
}

public class DrainageObstruction : EntityBase
{
    public string Reference { get; set; } = string.Empty;
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public Point? Geometry { get; set; }
    public string? WaterwayName { get; set; }
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Medium;
    public string Status { get; set; } = "Reported";
    public string? ReportedBy { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? AdminAreaId { get; set; }
    public AdminArea? AdminArea { get; set; }
}
