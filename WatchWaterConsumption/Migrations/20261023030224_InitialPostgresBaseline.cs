using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WatchWaterConsumption.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Parameter = table.Column<string>(type: "text", nullable: false),
                    Operator = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Threshold = table.Column<decimal>(type: "numeric(18,5)", precision: 18, scale: 5, nullable: true),
                    Threshold2 = table.Column<decimal>(type: "numeric(18,5)", precision: 18, scale: 5, nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EscalationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Organisation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsumptionImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    RowsTotal = table.Column<int>(type: "integer", nullable: false),
                    RowsAccepted = table.Column<int>(type: "integer", nullable: false),
                    RowsRejected = table.Column<int>(type: "integer", nullable: false),
                    ErrorsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumptionImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Consumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Neighbourhood = table.Column<string>(type: "text", nullable: true),
                    SuburbGroup = table.Column<string>(type: "text", nullable: true),
                    AverageMonthlyKL = table.Column<int>(type: "integer", nullable: true),
                    Stroke = table.Column<string>(type: "text", nullable: true),
                    StrokeWidth = table.Column<string>(type: "text", nullable: true),
                    StrokeOpacity = table.Column<string>(type: "text", nullable: true),
                    Fill = table.Column<string>(type: "text", nullable: true),
                    FillOpacity = table.Column<string>(type: "text", nullable: true),
                    Coordinates = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consumptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Licence = table.Column<string>(type: "text", nullable: true),
                    LicenceUrl = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Coverage = table.Column<string>(type: "text", nullable: true),
                    QualityNotes = table.Column<string>(type: "text", nullable: true),
                    UpdateFrequency = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsOfficial = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloodEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    IsOfficial = table.Column<bool>(type: "boolean", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloodEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualityStandards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Authority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ReferenceUrl = table.Column<string>(type: "text", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityStandards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ReportType = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ResultPath = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SamplingCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: true),
                    LaboratoryName = table.Column<string>(type: "text", nullable: true),
                    LaboratoryAccredited = table.Column<bool>(type: "boolean", nullable: false),
                    AccreditationReference = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplingCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SatelliteScenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    Collection = table.Column<string>(type: "text", nullable: false),
                    SceneId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CloudCover = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    ResolutionM = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    Footprint = table.Column<Geometry>(type: "geometry (Geometry, 4326)", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    AssetsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Licence = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    RetrievedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatelliteScenes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Geometry = table.Column<Geometry>(type: "geometry (Geometry, 4326)", nullable: true),
                    AreaSqKm = table.Column<double>(type: "double precision", nullable: true),
                    Population = table.Column<long>(type: "bigint", nullable: true),
                    PopulationYear = table.Column<int>(type: "integer", nullable: true),
                    PopulationSource = table.Column<string>(type: "text", nullable: true),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAreas_AdminAreas_ParentId",
                        column: x => x.ParentId,
                        principalTable: "AdminAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminAreas_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    TargetLayerKey = table.Column<string>(type: "text", nullable: true),
                    Format = table.Column<string>(type: "text", nullable: true),
                    RequestedBy = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RowsTotal = table.Column<int>(type: "integer", nullable: false),
                    RowsAccepted = table.Column<int>(type: "integer", nullable: false),
                    RowsRejected = table.Column<int>(type: "integer", nullable: false),
                    ErrorsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: true),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OperationalAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AreaType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Geometry = table.Column<Geometry>(type: "geometry (Geometry, 4326)", nullable: true),
                    AreaSqKm = table.Column<double>(type: "double precision", nullable: true),
                    Population = table.Column<long>(type: "bigint", nullable: true),
                    IsOfficial = table.Column<bool>(type: "boolean", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalAreas_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OperationalAreas_OperationalAreas_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OperationalAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpatialLayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    GeometryType = table.Column<string>(type: "text", nullable: false),
                    StyleJson = table.Column<string>(type: "jsonb", nullable: true),
                    ServiceType = table.Column<string>(type: "text", nullable: true),
                    ServiceUrl = table.Column<string>(type: "text", nullable: true),
                    ServiceLayerName = table.Column<string>(type: "text", nullable: true),
                    FeatureCount = table.Column<int>(type: "integer", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisibleByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    MinZoom = table.Column<int>(type: "integer", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpatialLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpatialLayers_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FloodLayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LayerType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Geometry = table.Column<Geometry>(type: "geometry (Geometry, 4326)", nullable: true),
                    IsOfficial = table.Column<bool>(type: "boolean", nullable: false),
                    Assumptions = table.Column<string>(type: "text", nullable: true),
                    Uncertainty = table.Column<string>(type: "text", nullable: true),
                    ValidationStatus = table.Column<string>(type: "text", nullable: true),
                    ModelVersion = table.Column<string>(type: "text", nullable: true),
                    FloodEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloodLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloodLayers_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FloodLayers_FloodEvents_FloodEventId",
                        column: x => x.FloodEventId,
                        principalTable: "FloodEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "QualityLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Parameter = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    MinValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ReferenceSection = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityLimits_QualityStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "QualityStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrainageObstructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Geometry = table.Column<Point>(type: "geometry (Point, 4326)", nullable: true),
                    WaterwayName = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReportedBy = table.Column<string>(type: "text", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrainageObstructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrainageObstructions_AdminAreas_AdminAreaId",
                        column: x => x.AdminAreaId,
                        principalTable: "AdminAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HydrologyStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Geometry = table.Column<Point>(type: "geometry (Point, 4326)", nullable: true),
                    WaterwayName = table.Column<string>(type: "text", nullable: true),
                    AdminAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Datum = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HydrologyStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HydrologyStations_AdminAreas_AdminAreaId",
                        column: x => x.AdminAreaId,
                        principalTable: "AdminAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HydrologyStations_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Geometry = table.Column<Geometry>(type: "geometry (Geometry, 4326)", nullable: true),
                    AdminAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationalAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Owner = table.Column<string>(type: "text", nullable: true),
                    Operator = table.Column<string>(type: "text", nullable: true),
                    InstalledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SpecificationsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CapacityM3PerDay = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    DesignParametersJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceReference = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_AdminAreas_AdminAreaId",
                        column: x => x.AdminAreaId,
                        principalTable: "AdminAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Assets_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Assets_OperationalAreas_OperationalAreaId",
                        column: x => x.OperationalAreaId,
                        principalTable: "OperationalAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConsumerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    AdminAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    DmaId = table.Column<Guid>(type: "uuid", nullable: true),
                    HouseholdSize = table.Column<int>(type: "integer", nullable: true),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumerAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumerAccounts_AdminAreas_AdminAreaId",
                        column: x => x.AdminAreaId,
                        principalTable: "AdminAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConsumerAccounts_OperationalAreas_DmaId",
                        column: x => x.DmaId,
                        principalTable: "OperationalAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConsumerAccounts_OperationalAreas_ServiceZoneId",
                        column: x => x.ServiceZoneId,
                        principalTable: "OperationalAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DmaWaterBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DmaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    InputVolumeM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    BilledVolumeM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnbilledAuthorisedM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    ApparentLossesM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    RealLossesM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    MinimumNightFlowM3PerHour = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Method = table.Column<string>(type: "text", nullable: true),
                    UncertaintyNotes = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmaWaterBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DmaWaterBalances_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DmaWaterBalances_OperationalAreas_DmaId",
                        column: x => x.DmaId,
                        principalTable: "OperationalAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SamplingStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WaterSourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Geometry = table.Column<Point>(type: "geometry (Point, 4326)", nullable: true),
                    AdminAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplingStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SamplingStations_AdminAreas_AdminAreaId",
                        column: x => x.AdminAreaId,
                        principalTable: "AdminAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SamplingStations_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SamplingStations_OperationalAreas_ServiceZoneId",
                        column: x => x.ServiceZoneId,
                        principalTable: "OperationalAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SpatialFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Geometry = table.Column<Geometry>(type: "geometry (Geometry, 4326)", nullable: false),
                    AttributesJson = table.Column<string>(type: "jsonb", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GeometryHash = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpatialFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpatialFeatures_SpatialLayers_LayerId",
                        column: x => x.LayerId,
                        principalTable: "SpatialLayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FloodExposureResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FloodLayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnalysedBy = table.Column<string>(type: "text", nullable: false),
                    AssetsExposed = table.Column<int>(type: "integer", nullable: false),
                    SamplingStationsExposed = table.Column<int>(type: "integer", nullable: false),
                    DevicesExposed = table.Column<int>(type: "integer", nullable: false),
                    PopulationExposed = table.Column<long>(type: "bigint", nullable: true),
                    Method = table.Column<string>(type: "text", nullable: true),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResultsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Limitations = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloodExposureResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloodExposureResults_FloodLayers_FloodLayerId",
                        column: x => x.FloodLayerId,
                        principalTable: "FloodLayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HydrologyObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Parameter = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    QualityFlag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HydrologyObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HydrologyObservations_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HydrologyObservations_HydrologyStations_StationId",
                        column: x => x.StationId,
                        principalTable: "HydrologyStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: true),
                    UploadedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDocuments_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoreholeDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoreholeDepthM = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ScreenTopM = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ScreenBottomM = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    AquiferName = table.Column<string>(type: "text", nullable: true),
                    AquiferClassification = table.Column<string>(type: "text", nullable: true),
                    AquiferClassificationInferred = table.Column<bool>(type: "boolean", nullable: false),
                    StaticWaterLevelM = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DynamicWaterLevelM = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    YieldM3PerHour = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    DrilledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Driller = table.Column<string>(type: "text", nullable: true),
                    ConstructionJson = table.Column<string>(type: "jsonb", nullable: true),
                    LithologyJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoreholeDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoreholeDetails_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    HydrologyStationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Geometry = table.Column<Point>(type: "geometry (Point, 4326)", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "text", nullable: true),
                    InstalledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ApiKeyHash = table.Column<string>(type: "text", nullable: true),
                    OfflineThresholdMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsDemoDevice = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Devices_HydrologyStations_HydrologyStationId",
                        column: x => x.HydrologyStationId,
                        principalTable: "HydrologyStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GroundwaterReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoreholeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WaterLevelM = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    AbstractionM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroundwaterReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroundwaterReadings_Assets_BoreholeAssetId",
                        column: x => x.BoreholeAssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroundwaterReadings_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Inspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InspectorName = table.Column<string>(type: "text", nullable: true),
                    Findings = table.Column<string>(type: "text", nullable: true),
                    ConditionAfter = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PhotoPathsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inspections_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PerformedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PerformedBy = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRecords_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Geometry = table.Column<Point>(type: "geometry (Point, 4326)", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkNodes_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PumpingTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoreholeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PumpingRateM3PerHour = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    DrawdownM = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    RecoveryMinutes = table.Column<int>(type: "integer", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ConductedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PumpingTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PumpingTests_Assets_BoreholeAssetId",
                        column: x => x.BoreholeAssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Geometry = table.Column<Point>(type: "geometry (Point, 4326)", nullable: true),
                    ReportedBy = table.Column<string>(type: "text", nullable: true),
                    AssignedTo = table.Column<string>(type: "text", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    VerificationNotes = table.Column<string>(type: "text", nullable: true),
                    ResolvedBy = table.Column<string>(type: "text", nullable: true),
                    VerifiedBy = table.Column<string>(type: "text", nullable: true),
                    ClosedBy = table.Column<string>(type: "text", nullable: true),
                    PhotoPathsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConsumerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstalledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Multiplier = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Manufacturer = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    RolloverValueM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meters_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Meters_ConsumerAccounts_ConsumerAccountId",
                        column: x => x.ConsumerAccountId,
                        principalTable: "ConsumerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WaterSamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SampleCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SampledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SampledBy = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterSamples_SamplingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "SamplingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaterSamples_SamplingStations_StationId",
                        column: x => x.StationId,
                        principalTable: "SamplingStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,5)", precision: 18, scale: 5, nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "text", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    EscalatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EscalationCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_AlertRules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "AlertRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alerts_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Parameter = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,5)", precision: 18, scale: 5, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Quality = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawPayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryReadings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    FromNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Geometry = table.Column<LineString>(type: "geometry (LineString, 4326)", nullable: true),
                    DiameterMm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Material = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    LengthM = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkEdges_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NetworkEdges_NetworkNodes_FromNodeId",
                        column: x => x.FromNodeId,
                        principalTable: "NetworkNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NetworkEdges_NetworkNodes_ToNodeId",
                        column: x => x.ToNodeId,
                        principalTable: "NetworkNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeakIncidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Geometry = table.Column<Point>(type: "geometry (Point, 4326)", nullable: true),
                    DmaId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EstimatedLossM3PerHour = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    EstimatedLossIsEstimate = table.Column<bool>(type: "boolean", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeakIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeakIncidents_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeakIncidents_OperationalAreas_DmaId",
                        column: x => x.DmaId,
                        principalTable: "OperationalAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeakIncidents_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedBy = table.Column<string>(type: "text", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderEvents_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReadingValueM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReadingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CorrectedFromValueM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    CorrectionReason = table.Column<string>(type: "text", nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterReadings_ConsumptionImports_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ConsumptionImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MeterReadings_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SampleResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SampleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Parameter = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    DetectionLimit = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Method = table.Column<string>(type: "text", nullable: true),
                    Qualifier = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AppliedLimitId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardCode = table.Column<string>(type: "text", nullable: true),
                    AssessmentNote = table.Column<string>(type: "text", nullable: true),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleResults_QualityLimits_AppliedLimitId",
                        column: x => x.AppliedLimitId,
                        principalTable: "QualityLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SampleResults_WaterSamples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "WaterSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAreas_Code",
                table: "AdminAreas",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminAreas_DataSourceId",
                table: "AdminAreas",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAreas_Geometry",
                table: "AdminAreas",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAreas_Level_ParentId",
                table: "AdminAreas",
                columns: new[] { "Level", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAreas_ParentId",
                table: "AdminAreas",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_AlertRuleId",
                table: "Alerts",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_DeviceId",
                table: "Alerts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Status_TriggeredAt",
                table: "Alerts",
                columns: new[] { "Status", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDocuments_AssetId",
                table: "AssetDocuments",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AdminAreaId",
                table: "Assets",
                column: "AdminAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetCode",
                table: "Assets",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetType_Status",
                table: "Assets",
                columns: new[] { "AssetType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DataSourceId",
                table: "Assets",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Geometry",
                table: "Assets",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Name",
                table: "Assets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_OperationalAreaId",
                table: "Assets",
                column: "OperationalAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OccurredAt",
                table: "AuditEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_BoreholeDetails_AssetId",
                table: "BoreholeDetails",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerAccounts_AccountNumber",
                table: "ConsumerAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerAccounts_AdminAreaId",
                table: "ConsumerAccounts",
                column: "AdminAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerAccounts_DmaId",
                table: "ConsumerAccounts",
                column: "DmaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerAccounts_ServiceZoneId",
                table: "ConsumerAccounts",
                column: "ServiceZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_Name",
                table: "DataSources",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_AssetId",
                table: "Devices",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceCode",
                table: "Devices",
                column: "DeviceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Geometry",
                table: "Devices",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_HydrologyStationId",
                table: "Devices",
                column: "HydrologyStationId");

            migrationBuilder.CreateIndex(
                name: "IX_DmaWaterBalances_DataSourceId",
                table: "DmaWaterBalances",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DmaWaterBalances_DmaId_PeriodStart",
                table: "DmaWaterBalances",
                columns: new[] { "DmaId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrainageObstructions_AdminAreaId",
                table: "DrainageObstructions",
                column: "AdminAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_DrainageObstructions_Geometry",
                table: "DrainageObstructions",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_FloodEvents_StartedAt",
                table: "FloodEvents",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FloodExposureResults_AnalysedAt",
                table: "FloodExposureResults",
                column: "AnalysedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FloodExposureResults_FloodLayerId",
                table: "FloodExposureResults",
                column: "FloodLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_FloodLayers_DataSourceId",
                table: "FloodLayers",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FloodLayers_FloodEventId",
                table: "FloodLayers",
                column: "FloodEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FloodLayers_Geometry",
                table: "FloodLayers",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_GroundwaterReadings_BoreholeAssetId_ObservedAt",
                table: "GroundwaterReadings",
                columns: new[] { "BoreholeAssetId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroundwaterReadings_DataSourceId",
                table: "GroundwaterReadings",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_HydrologyObservations_DataSourceId",
                table: "HydrologyObservations",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_HydrologyObservations_ObservedAt",
                table: "HydrologyObservations",
                column: "ObservedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HydrologyObservations_StationId_Parameter_ObservedAt",
                table: "HydrologyObservations",
                columns: new[] { "StationId", "Parameter", "ObservedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HydrologyStations_AdminAreaId",
                table: "HydrologyStations",
                column: "AdminAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_HydrologyStations_Code",
                table: "HydrologyStations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HydrologyStations_DataSourceId",
                table: "HydrologyStations",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_HydrologyStations_Geometry",
                table: "HydrologyStations",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_DataSourceId",
                table: "ImportBatches",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_AssetId",
                table: "Inspections",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_ScheduledFor",
                table: "Inspections",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_LeakIncidents_AssetId",
                table: "LeakIncidents",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_LeakIncidents_DmaId",
                table: "LeakIncidents",
                column: "DmaId");

            migrationBuilder.CreateIndex(
                name: "IX_LeakIncidents_Geometry",
                table: "LeakIncidents",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_LeakIncidents_Reference",
                table: "LeakIncidents",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeakIncidents_WorkOrderId",
                table: "LeakIncidents",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_AssetId",
                table: "MaintenanceRecords",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_PerformedAt",
                table: "MaintenanceRecords",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_ImportBatchId",
                table: "MeterReadings",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_MeterId_ReadingDate",
                table: "MeterReadings",
                columns: new[] { "MeterId", "ReadingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_ReadingDate",
                table: "MeterReadings",
                column: "ReadingDate");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_AssetId",
                table: "Meters",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_ConsumerAccountId",
                table: "Meters",
                column: "ConsumerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_SerialNumber",
                table: "Meters",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEdges_AssetId",
                table: "NetworkEdges",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEdges_Code",
                table: "NetworkEdges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEdges_FromNodeId",
                table: "NetworkEdges",
                column: "FromNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEdges_Geometry",
                table: "NetworkEdges",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEdges_ToNodeId",
                table: "NetworkEdges",
                column: "ToNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkNodes_AssetId",
                table: "NetworkNodes",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkNodes_Code",
                table: "NetworkNodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkNodes_Geometry",
                table: "NetworkNodes",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAreas_Code",
                table: "OperationalAreas",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAreas_DataSourceId",
                table: "OperationalAreas",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAreas_Geometry",
                table: "OperationalAreas",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAreas_ParentId",
                table: "OperationalAreas",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_PumpingTests_BoreholeAssetId",
                table: "PumpingTests",
                column: "BoreholeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PumpingTests_TestedAt",
                table: "PumpingTests",
                column: "TestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QualityLimits_StandardId_Parameter",
                table: "QualityLimits",
                columns: new[] { "StandardId", "Parameter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityStandards_Code",
                table: "QualityStandards",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SampleResults_AppliedLimitId",
                table: "SampleResults",
                column: "AppliedLimitId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleResults_Parameter_State",
                table: "SampleResults",
                columns: new[] { "Parameter", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleResults_SampleId_Parameter",
                table: "SampleResults",
                columns: new[] { "SampleId", "Parameter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SamplingCampaigns_Code",
                table: "SamplingCampaigns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SamplingStations_AdminAreaId",
                table: "SamplingStations",
                column: "AdminAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplingStations_Code",
                table: "SamplingStations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SamplingStations_DataSourceId",
                table: "SamplingStations",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplingStations_Geometry",
                table: "SamplingStations",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_SamplingStations_ServiceZoneId",
                table: "SamplingStations",
                column: "ServiceZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_SatelliteScenes_AcquiredAt",
                table: "SatelliteScenes",
                column: "AcquiredAt");

            migrationBuilder.CreateIndex(
                name: "IX_SatelliteScenes_Provider_SceneId",
                table: "SatelliteScenes",
                columns: new[] { "Provider", "SceneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpatialFeatures_Geometry",
                table: "SpatialFeatures",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_SpatialFeatures_LayerId_ExternalId",
                table: "SpatialFeatures",
                columns: new[] { "LayerId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpatialFeatures_LayerId_GeometryHash",
                table: "SpatialFeatures",
                columns: new[] { "LayerId", "GeometryHash" });

            migrationBuilder.CreateIndex(
                name: "IX_SpatialLayers_DataSourceId",
                table: "SpatialLayers",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SpatialLayers_Key",
                table: "SpatialLayers",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryReadings_DeviceId_ObservedAt_Parameter",
                table: "TelemetryReadings",
                columns: new[] { "DeviceId", "ObservedAt", "Parameter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryReadings_ObservedAt",
                table: "TelemetryReadings",
                column: "ObservedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WaterSamples_CampaignId",
                table: "WaterSamples",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_WaterSamples_SampleCode",
                table: "WaterSamples",
                column: "SampleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaterSamples_SampledAt",
                table: "WaterSamples",
                column: "SampledAt");

            migrationBuilder.CreateIndex(
                name: "IX_WaterSamples_StationId",
                table: "WaterSamples",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEvents_ChangedAt",
                table: "WorkOrderEvents",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEvents_WorkOrderId",
                table: "WorkOrderEvents",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssetId",
                table: "WorkOrders",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_Geometry",
                table: "WorkOrders",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_Reference",
                table: "WorkOrders",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_Status_Priority",
                table: "WorkOrders",
                columns: new[] { "Status", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AssetDocuments");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "BoreholeDetails");

            migrationBuilder.DropTable(
                name: "Consumptions");

            migrationBuilder.DropTable(
                name: "DmaWaterBalances");

            migrationBuilder.DropTable(
                name: "DrainageObstructions");

            migrationBuilder.DropTable(
                name: "FloodExposureResults");

            migrationBuilder.DropTable(
                name: "GroundwaterReadings");

            migrationBuilder.DropTable(
                name: "HydrologyObservations");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "Inspections");

            migrationBuilder.DropTable(
                name: "LeakIncidents");

            migrationBuilder.DropTable(
                name: "MaintenanceRecords");

            migrationBuilder.DropTable(
                name: "MeterReadings");

            migrationBuilder.DropTable(
                name: "NetworkEdges");

            migrationBuilder.DropTable(
                name: "PumpingTests");

            migrationBuilder.DropTable(
                name: "ReportJobs");

            migrationBuilder.DropTable(
                name: "SampleResults");

            migrationBuilder.DropTable(
                name: "SatelliteScenes");

            migrationBuilder.DropTable(
                name: "SpatialFeatures");

            migrationBuilder.DropTable(
                name: "TelemetryReadings");

            migrationBuilder.DropTable(
                name: "WorkOrderEvents");

            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "FloodLayers");

            migrationBuilder.DropTable(
                name: "ConsumptionImports");

            migrationBuilder.DropTable(
                name: "Meters");

            migrationBuilder.DropTable(
                name: "NetworkNodes");

            migrationBuilder.DropTable(
                name: "QualityLimits");

            migrationBuilder.DropTable(
                name: "WaterSamples");

            migrationBuilder.DropTable(
                name: "SpatialLayers");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "FloodEvents");

            migrationBuilder.DropTable(
                name: "ConsumerAccounts");

            migrationBuilder.DropTable(
                name: "QualityStandards");

            migrationBuilder.DropTable(
                name: "SamplingCampaigns");

            migrationBuilder.DropTable(
                name: "SamplingStations");

            migrationBuilder.DropTable(
                name: "HydrologyStations");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "AdminAreas");

            migrationBuilder.DropTable(
                name: "OperationalAreas");

            migrationBuilder.DropTable(
                name: "DataSources");
        }
    }
}
