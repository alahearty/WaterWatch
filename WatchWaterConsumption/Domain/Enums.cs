namespace WaterWatch.Domain.Enums;

public enum DataClassification
{
    Observed = 0,
    Imported = 1,
    Estimated = 2,
    Modelled = 3,
    SyntheticDemonstration = 4
}

public enum AdminLevel
{
    Country,
    State,
    Lga,
    Ward,
    Settlement
}

public enum OperationalAreaType
{
    ServiceZone,
    DistrictMeteredArea,
    Catchment
}

public enum AssetType
{
    Borehole,
    Well,
    TreatmentPlant,
    Reservoir,
    ElevatedTank,
    GroundStorageTank,
    PumpingStation,
    DistributionMain,
    TransmissionMain,
    Valve,
    Hydrant,
    WaterMeter,
    PressureMonitoringPoint,
    SamplingStation,
    DrainageAsset,
    Other
}

public enum AssetStatus
{
    Operational,
    UnderMaintenance,
    OutOfService,
    Decommissioned,
    Unknown
}

public enum ConditionGrade
{
    Excellent,
    Good,
    Fair,
    Poor,
    Critical,
    Unknown
}

public enum ConsumerCategory
{
    Domestic,
    Commercial,
    Industrial,
    Institutional,
    Public
}

public enum MeterStatus
{
    Active,
    Inactive,
    Faulty,
    Removed
}

public enum ReadingType
{
    Observed,
    Estimated,
    Corrected,
    Rollover
}

public enum WaterSourceType
{
    Groundwater,
    SurfaceWater,
    TreatedSupply,
    DistributedSupply
}

public enum ParameterCode
{
    pH,
    TemperatureC,
    TurbidityNTU,
    ElectricalConductivityUScm,
    TotalDissolvedSolidsMgL,
    DissolvedOxygenMgL,
    BOD5MgL,
    CODMgL,
    NitrateMgL,
    ChlorideMgL,
    IronMgL,
    LeadMgL,
    TotalColiformCfu100ml,
    EColiCfu100ml,
    HydrocarbonMgL
}

public enum QualityState
{
    Compliant,
    NonCompliant,
    InsufficientEvidence,
    NotAssessed
}

public enum HydrologyStationType
{
    Rainfall,
    WaterLevel,
    Streamflow,
    Tide
}

public enum HydrologyParameter
{
    RainfallMm,
    WaterLevelM,
    StreamflowM3s,
    TideLevelM
}

public enum QualityFlag
{
    Observed,
    Estimated,
    Provisional,
    Missing
}

public enum FloodLayerType
{
    Observed,
    Historical,
    ModelledSusceptibility,
    Forecast,
    OfficialWarning
}

public enum Severity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

public enum DeviceType
{
    WaterLevel,
    Flow,
    Pressure,
    WaterQuality,
    RainGauge,
    PumpTelemetry,
    ReservoirLevel
}

public enum DeviceStatus
{
    Active,
    Offline,
    Maintenance,
    Retired
}

public enum TelemetryQuality
{
    Observed,
    Estimated,
    Suspect,
    Missing,
    SyntheticDemonstration
}

public enum AlertStatus
{
    Open,
    Acknowledged,
    Resolved
}

public enum AlertOperator
{
    GreaterThan,
    LessThan,
    OutsideRange,
    NoData
}

public enum WorkOrderStatus
{
    Reported,
    Triaged,
    Assigned,
    InProgress,
    Resolved,
    Verified,
    Closed,
    Cancelled
}

public enum WorkOrderType
{
    Inspection,
    LeakRepair,
    Maintenance,
    Defect,
    Other
}

public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

public enum SamplingCampaignStatus
{
    Planned,
    Active,
    Completed,
    Cancelled
}

public enum LeakStatus
{
    Reported,
    Triaged,
    Assigned,
    InProgress,
    Repaired,
    Verified,
    Closed
}

public enum ReportFormat
{
    Csv,
    Xlsx,
    Pdf
}

public enum ImportStatus
{
    Preview,
    Imported,
    Failed,
    PartiallyImported
}

public enum NetworkNodeType
{
    Source,
    Treatment,
    Pump,
    Tank,
    Junction,
    Valve,
    Demand
}
