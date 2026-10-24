using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WatchWaterConsumption.Models;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Data;

public class DatabaseContext : IdentityDbContext<ApplicationUser>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DatabaseContext(
        DbContextOptions<DatabaseContext> options,
        IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<WaterConsumption> Consumptions => Set<WaterConsumption>();

    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<AdminArea> AdminAreas => Set<AdminArea>();
    public DbSet<OperationalArea> OperationalAreas => Set<OperationalArea>();
    public DbSet<SpatialLayer> SpatialLayers => Set<SpatialLayer>();
    public DbSet<SpatialFeature> SpatialFeatures => Set<SpatialFeature>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetDocument> AssetDocuments => Set<AssetDocument>();
    public DbSet<InspectionRecord> Inspections => Set<InspectionRecord>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<NetworkNode> NetworkNodes => Set<NetworkNode>();
    public DbSet<NetworkEdge> NetworkEdges => Set<NetworkEdge>();

    public DbSet<ConsumerAccount> ConsumerAccounts => Set<ConsumerAccount>();
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<ConsumptionImport> ConsumptionImports => Set<ConsumptionImport>();

    public DbSet<SamplingStation> SamplingStations => Set<SamplingStation>();
    public DbSet<QualityStandard> QualityStandards => Set<QualityStandard>();
    public DbSet<QualityLimit> QualityLimits => Set<QualityLimit>();
    public DbSet<SamplingCampaign> SamplingCampaigns => Set<SamplingCampaign>();
    public DbSet<WaterSample> WaterSamples => Set<WaterSample>();
    public DbSet<SampleResult> SampleResults => Set<SampleResult>();

    public DbSet<HydrologyStation> HydrologyStations => Set<HydrologyStation>();
    public DbSet<HydrologyObservation> HydrologyObservations => Set<HydrologyObservation>();
    public DbSet<DrainageObstruction> DrainageObstructions => Set<DrainageObstruction>();
    public DbSet<FloodEvent> FloodEvents => Set<FloodEvent>();
    public DbSet<FloodLayer> FloodLayers => Set<FloodLayer>();
    public DbSet<FloodExposureResult> FloodExposureResults => Set<FloodExposureResult>();

    public DbSet<BoreholeDetail> BoreholeDetails => Set<BoreholeDetail>();
    public DbSet<PumpingTest> PumpingTests => Set<PumpingTest>();
    public DbSet<GroundwaterReading> GroundwaterReadings => Set<GroundwaterReading>();

    public DbSet<DmaWaterBalance> DmaWaterBalances => Set<DmaWaterBalance>();
    public DbSet<LeakIncident> LeakIncidents => Set<LeakIncident>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<TelemetryReading> TelemetryReadings => Set<TelemetryReading>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<SatelliteScene> SatelliteScenes => Set<SatelliteScene>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderEvent> WorkOrderEvents => Set<WorkOrderEvent>();
    public DbSet<ReportJob> ReportJobs => Set<ReportJob>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Organisation).HasMaxLength(160);
        });

        modelBuilder.Entity<WaterConsumption>(entity =>
        {
            entity.ToTable("Consumptions");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<DataSource>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Publisher).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<AdminArea>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Alias).HasMaxLength(200);
            entity.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Geometry, 4326)");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasIndex(x => new { x.Level, x.ParentId });
            entity.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OperationalArea>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AreaType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Geometry, 4326)");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SpatialLayer>(entity =>
        {
            entity.Property(x => x.Key).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.StyleJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.Key).IsUnique();
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SpatialFeature>(entity =>
        {
            entity.Property(x => x.ExternalId).HasMaxLength(200);
            entity.Property(x => x.Name).HasMaxLength(250);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Geometry, 4326)");
            entity.Property(x => x.AttributesJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasIndex(x => new { x.LayerId, x.ExternalId });
            entity.HasIndex(x => new { x.LayerId, x.GeometryHash });
            entity.HasOne(x => x.Layer).WithMany().HasForeignKey(x => x.LayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ErrorsJson).HasColumnType("jsonb");
            entity.Property(x => x.ParametersJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.Property(x => x.AssetCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AssetType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Condition).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Geometry, 4326)");
            entity.Property(x => x.SpecificationsJson).HasColumnType("jsonb");
            entity.Property(x => x.DesignParametersJson).HasColumnType("jsonb");
            entity.Property(x => x.CapacityM3PerDay).HasPrecision(18, 3);
            entity.HasIndex(x => x.AssetCode).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasIndex(x => new { x.AssetType, x.Status });
            entity.HasIndex(x => x.Name);
            entity.HasOne(x => x.AdminArea).WithMany().HasForeignKey(x => x.AdminAreaId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.OperationalArea).WithMany().HasForeignKey(x => x.OperationalAreaId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssetDocument>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Asset).WithMany(a => a.Documents).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InspectionRecord>(entity =>
        {
            entity.Property(x => x.ConditionAfter).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.PhotoPathsJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Asset).WithMany(a => a.Inspections).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.ScheduledFor);
        });

        modelBuilder.Entity<MaintenanceRecord>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Asset).WithMany(a => a.MaintenanceRecords).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.PerformedAt);
        });

        modelBuilder.Entity<NetworkNode>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.NodeType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Point, 4326)");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NetworkEdge>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Geometry).HasColumnType("geometry (LineString, 4326)");
            entity.Property(x => x.DiameterMm).HasPrecision(10, 2);
            entity.Property(x => x.LengthM).HasPrecision(18, 2);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.FromNode).WithMany().HasForeignKey(x => x.FromNodeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToNode).WithMany().HasForeignKey(x => x.ToNodeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ConsumerAccount>(entity =>
        {
            entity.Property(x => x.AccountNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.AccountNumber).IsUnique();
            entity.HasOne(x => x.AdminArea).WithMany().HasForeignKey(x => x.AdminAreaId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ServiceZone).WithMany().HasForeignKey(x => x.ServiceZoneId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Dma).WithMany().HasForeignKey(x => x.DmaId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Meter>(entity =>
        {
            entity.Property(x => x.SerialNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Multiplier).HasPrecision(12, 4);
            entity.Property(x => x.RolloverValueM3).HasPrecision(18, 3);
            entity.HasIndex(x => x.SerialNumber).IsUnique();
            entity.HasOne(x => x.ConsumerAccount).WithMany(a => a.Meters).HasForeignKey(x => x.ConsumerAccountId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MeterReading>(entity =>
        {
            entity.Property(x => x.ReadingType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ReadingValueM3).HasPrecision(18, 3);
            entity.Property(x => x.CorrectedFromValueM3).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.MeterId, x.ReadingDate }).IsUnique();
            entity.HasIndex(x => x.ReadingDate);
            entity.HasOne(x => x.Meter).WithMany(m => m.Readings).HasForeignKey(x => x.MeterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ImportBatch).WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ConsumptionImport>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ErrorsJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<SamplingStation>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.WaterSourceType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Point, 4326)");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.AdminArea).WithMany().HasForeignKey(x => x.AdminAreaId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ServiceZone).WithMany().HasForeignKey(x => x.ServiceZoneId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QualityStandard>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Authority).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Version).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<QualityLimit>(entity =>
        {
            entity.Property(x => x.Parameter).HasConversion<string>().HasMaxLength(60);
            entity.Property(x => x.MinValue).HasPrecision(18, 6);
            entity.Property(x => x.MaxValue).HasPrecision(18, 6);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.StandardId, x.Parameter }).IsUnique();
            entity.HasOne(x => x.Standard).WithMany(s => s.Limits).HasForeignKey(x => x.StandardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SamplingCampaign>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<WaterSample>(entity =>
        {
            entity.Property(x => x.SampleCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.SampleCode).IsUnique();
            entity.HasIndex(x => x.SampledAt);
            entity.HasOne(x => x.Station).WithMany(s => s.Samples).HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Campaign).WithMany(c => c.Samples).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SampleResult>(entity =>
        {
            entity.Property(x => x.Parameter).HasConversion<string>().HasMaxLength(60);
            entity.Property(x => x.State).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Value).HasPrecision(18, 6);
            entity.Property(x => x.DetectionLimit).HasPrecision(18, 6);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.SampleId, x.Parameter }).IsUnique();
            entity.HasIndex(x => new { x.Parameter, x.State });
            entity.HasOne(x => x.Sample).WithMany(s => s.Results).HasForeignKey(x => x.SampleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AppliedLimit).WithMany().HasForeignKey(x => x.AppliedLimitId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<HydrologyStation>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.StationType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Point, 4326)");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.AdminArea).WithMany().HasForeignKey(x => x.AdminAreaId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<HydrologyObservation>(entity =>
        {
            entity.Property(x => x.Parameter).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.QualityFlag).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Value).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.StationId, x.Parameter, x.ObservedAt }).IsUnique();
            entity.HasIndex(x => x.ObservedAt);
            entity.HasOne(x => x.Station).WithMany(s => s.Observations).HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DrainageObstruction>(entity =>
        {
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Point, 4326)");
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.AdminArea).WithMany().HasForeignKey(x => x.AdminAreaId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FloodEvent>(entity =>
        {
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<FloodLayer>(entity =>
        {
            entity.Property(x => x.LayerType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Geometry, 4326)");
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.FloodEvent).WithMany().HasForeignKey(x => x.FloodEventId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FloodExposureResult>(entity =>
        {
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ParametersJson).HasColumnType("jsonb");
            entity.Property(x => x.ResultsJson).HasColumnType("jsonb");
            entity.HasOne(x => x.FloodLayer).WithMany().HasForeignKey(x => x.FloodLayerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.AnalysedAt);
        });

        modelBuilder.Entity<BoreholeDetail>(entity =>
        {
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.BoreholeDepthM).HasPrecision(10, 2);
            entity.Property(x => x.ScreenTopM).HasPrecision(10, 2);
            entity.Property(x => x.ScreenBottomM).HasPrecision(10, 2);
            entity.Property(x => x.StaticWaterLevelM).HasPrecision(10, 2);
            entity.Property(x => x.DynamicWaterLevelM).HasPrecision(10, 2);
            entity.Property(x => x.YieldM3PerHour).HasPrecision(12, 3);
            entity.Property(x => x.ConstructionJson).HasColumnType("jsonb");
            entity.Property(x => x.LithologyJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.AssetId).IsUnique();
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PumpingTest>(entity =>
        {
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.PumpingRateM3PerHour).HasPrecision(12, 3);
            entity.Property(x => x.DrawdownM).HasPrecision(10, 3);
            entity.Property(x => x.ResultJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Borehole).WithMany().HasForeignKey(x => x.BoreholeAssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.TestedAt);
        });

        modelBuilder.Entity<GroundwaterReading>(entity =>
        {
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.WaterLevelM).HasPrecision(10, 3);
            entity.Property(x => x.AbstractionM3).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.BoreholeAssetId, x.ObservedAt });
            entity.HasOne(x => x.Borehole).WithMany().HasForeignKey(x => x.BoreholeAssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DmaWaterBalance>(entity =>
        {
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.InputVolumeM3).HasPrecision(18, 3);
            entity.Property(x => x.BilledVolumeM3).HasPrecision(18, 3);
            entity.Property(x => x.UnbilledAuthorisedM3).HasPrecision(18, 3);
            entity.Property(x => x.ApparentLossesM3).HasPrecision(18, 3);
            entity.Property(x => x.RealLossesM3).HasPrecision(18, 3);
            entity.Property(x => x.MinimumNightFlowM3PerHour).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.DmaId, x.PeriodStart }).IsUnique();
            entity.HasOne(x => x.Dma).WithMany().HasForeignKey(x => x.DmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LeakIncident>(entity =>
        {
            entity.Property(x => x.Reference).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.EstimatedLossM3PerHour).HasPrecision(12, 3);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Point, 4326)");
            entity.HasIndex(x => x.Reference).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.Dma).WithMany().HasForeignKey(x => x.DmaId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(x => x.DeviceCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DeviceType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Point, 4326)");
            entity.HasIndex(x => x.DeviceCode).IsUnique();
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.HydrologyStation).WithMany().HasForeignKey(x => x.HydrologyStationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TelemetryReading>(entity =>
        {
            entity.Property(x => x.Quality).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Value).HasPrecision(18, 5);
            entity.Property(x => x.RawPayloadJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.DeviceId, x.ObservedAt, x.Parameter }).IsUnique();
            entity.HasIndex(x => x.ObservedAt);
            entity.HasOne(x => x.Device).WithMany(d => d.Readings).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.Property(x => x.DeviceType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Operator).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Threshold).HasPrecision(18, 5);
            entity.Property(x => x.Threshold2).HasPrecision(18, 5);
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Value).HasPrecision(18, 5);
            entity.HasIndex(x => new { x.Status, x.TriggeredAt });
            entity.HasOne(x => x.AlertRule).WithMany().HasForeignKey(x => x.AlertRuleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SatelliteScene>(entity =>
        {
            entity.Property(x => x.SceneId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.CloudCover).HasPrecision(6, 2);
            entity.Property(x => x.ResolutionM).HasPrecision(10, 3);
            entity.Property(x => x.Footprint).HasColumnType("geometry (Geometry, 4326)");
            entity.Property(x => x.AssetsJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.Provider, x.SceneId }).IsUnique();
            entity.HasIndex(x => x.AcquiredAt);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.Property(x => x.Reference).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Geometry).HasColumnType("geometry (Point, 4326)");
            entity.HasIndex(x => x.Reference).IsUnique();
            entity.HasIndex(x => new { x.Status, x.Priority });
            entity.HasIndex(x => x.Geometry).HasMethod("gist");
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrderEvent>(entity =>
        {
            entity.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.WorkOrder).WithMany(w => w.Events).HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.ChangedAt);
        });

        modelBuilder.Entity<ReportJob>(entity =>
        {
            entity.Property(x => x.Format).HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.DataClassification).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ParametersJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.DetailsJson).HasColumnType("jsonb");
            entity.Property(x => x.UserName).HasMaxLength(256);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
        });
    }

    public override int SaveChanges()
    {
        PrepareChanges();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void PrepareChanges()
    {
        ChangeTracker.DetectChanges();
        var now = DateTimeOffset.UtcNow;
        var userName = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
        var ipAddress = _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        var correlationId = _httpContextAccessor?.HttpContext?.TraceIdentifier;

        var auditEntries = new List<AuditEntry>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditEntry)
            {
                continue;
            }

            if (entry.Entity is EntityBase entity && entry.State is EntityState.Added or EntityState.Modified)
            {
                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = now;
                }
                entity.UpdatedAt = now;
            }

            // Npgsql timestamptz requires UTC values. Normalise every DateTimeOffset
            // supplied by a client or a seed so offsets are never silently rejected.
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                foreach (var property in entry.Properties)
                {
                    if (property.CurrentValue is DateTimeOffset dateTimeOffset)
                    {
                        property.CurrentValue = dateTimeOffset.ToUniversalTime();
                    }
                }
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            // High-volume sensor readings are audited through their ingestion batch,
            // not one audit row per reading.
            if (entry.Entity is TelemetryReading)
            {
                continue;
            }

            var changed = entry.Properties
                .Where(p => entry.State != EntityState.Modified || p.IsModified)
                .Select(p => p.Metadata.Name)
                .ToArray();

            auditEntries.Add(new AuditEntry
            {
                OccurredAt = now,
                UserName = userName,
                Action = entry.State switch
                {
                    EntityState.Added => "Insert",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => "Unknown"
                },
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                DetailsJson = JsonSerializer.Serialize(new { changed }),
                IpAddress = ipAddress,
                CorrelationId = correlationId
            });
        }

        if (auditEntries.Count > 0)
        {
            AuditEntries.AddRange(auditEntries);
        }
    }
}
