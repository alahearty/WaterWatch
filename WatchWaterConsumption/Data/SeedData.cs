using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Triangulate;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Data;

public static class SeedData
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var importService = scope.ServiceProvider.GetRequiredService<IImportService>();
        var geoJson = scope.ServiceProvider.GetRequiredService<IGeoJsonService>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        await EnsureRolesAsync(roleManager);
        await EnsureUsersAsync(userManager, configuration, logger);
        await EnsureStandardsAsync(db);

        var sources = await EnsureDataSourcesAsync(db);
        await EnsureAdministrativeAreasAsync(db, geoJson, environment, sources, logger);
        await EnsureOsmLayersAsync(db, importService, geoJson, environment, sources, logger);
        await EnsureOsmAssetsAsync(db, geoJson, environment, sources, logger);

        if (configuration.GetValue("WaterWatch:DemoMode", false))
        {
            await EnsureDemoDataAsync(db, geoJson, sources, logger);
        }
        else
        {
            logger.LogInformation("Demo mode is disabled; no synthetic demonstration records were seeded.");
        }

        logger.LogInformation("WaterWatch seeding complete. Admin areas: {Areas}, assets: {Assets}, demo accounts: {Accounts}.",
            await db.AdminAreas.CountAsync(), await db.Assets.CountAsync(), await db.ConsumerAccounts.CountAsync());
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task EnsureUsersAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration, ILogger logger)
    {
        var demoMode = configuration.GetValue("WaterWatch:DemoMode", false);
        var password = configuration["WaterWatch:DemoPassword"] ?? "WaterWatch#2026";
        if (!demoMode)
        {
            var adminEmail = configuration["WaterWatch:AdminEmail"];
            var adminPassword = configuration["WaterWatch:AdminPassword"];
            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword) &&
                await userManager.FindByEmailAsync(adminEmail) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail, Email = adminEmail, EmailConfirmed = true,
                    DisplayName = "Bootstrap Administrator", Organisation = "WaterWatch"
                };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, Roles.Administrator);
                    logger.LogInformation("Created bootstrap administrator {Email}.", adminEmail);
                }
            }
            return;
        }

        var users = new (string Email, string Name, string Role)[]
        {
            ("admin@waterwatch.local", "Administrator", Roles.Administrator),
            ("analyst@waterwatch.local", "GIS Analyst", Roles.GisAnalyst),
            ("engineer@waterwatch.local", "Water Utility Engineer", Roles.WaterUtilityEngineer),
            ("hydrologist@waterwatch.local", "Hydrologist", Roles.Hydrologist),
            ("environment@waterwatch.local", "Environmental Officer", Roles.EnvironmentalOfficer),
            ("inspector@waterwatch.local", "Field Inspector", Roles.FieldInspector),
            ("viewer@waterwatch.local", "Management Viewer", Roles.ManagementViewer),
            ("public@waterwatch.local", "Public Viewer", Roles.PublicViewer)
        };
        foreach (var (email, name, role) in users)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email, Email = email, EmailConfirmed = true,
                    DisplayName = name, Organisation = "WaterWatch demonstration"
                };
                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    logger.LogWarning("Could not create demo user {Email}: {Errors}", email,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                    continue;
                }
            }
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    private static async Task<Dictionary<string, DataSource>> EnsureDataSourcesAsync(DatabaseContext db)
    {
        var definitions = new[]
        {
            new DataSource
            {
                Name = "OCHA COD-AB Nigeria", Publisher = "OCHA / HDX",
                Licence = "CC BY-IGO", LicenceUrl = "https://creativecommons.org/licenses/by/3.0/igo/",
                SourceUrl = "https://data.humdata.org/dataset/cod-ab-nga",
                AcquiredAt = DateTimeOffset.Parse("2026-10-23T00:00:00Z"),
                Coverage = "Nigeria admin 0–2; Port Harcourt and Obio/Akpor LGAs used",
                QualityNotes = "Cartographic administrative boundaries; valid_on 2019-04-17. Not cadastral or utility boundaries.",
                UpdateFrequency = "Periodic COD releases", IsOfficial = true,
                DataClassification = DataClassification.Imported
            },
            new DataSource
            {
                Name = "OpenStreetMap", Publisher = "OpenStreetMap contributors",
                Licence = "ODbL 1.0", LicenceUrl = "https://www.openstreetmap.org/copyright",
                SourceUrl = "https://www.openstreetmap.org/",
                AcquiredAt = DateTimeOffset.Parse("2026-10-23T00:37:05Z"),
                Coverage = "Port Harcourt / Obio-Akpor bbox (4.70,6.90,4.98,7.12)",
                QualityNotes = "Volunteered geographic data. Incomplete coverage; water assets are sparse. Mapped centrelines are not engineering surveys.",
                UpdateFrequency = "Continuous", IsOfficial = false,
                DataClassification = DataClassification.Imported
            },
            new DataSource
            {
                Name = "OCHA COD-PS Nigeria", Publisher = "OCHA / HDX",
                Licence = "CC BY-IGO", LicenceUrl = "https://creativecommons.org/licenses/by/3.0/igo/",
                SourceUrl = "https://data.humdata.org/dataset/cod-ps-nga",
                AcquiredAt = DateTimeOffset.Parse("2026-10-23T00:00:00Z"),
                Coverage = "Rivers State population projection 2022",
                QualityNotes = "Projection from the 2006 census. Admin-2 population is not published in this package.",
                UpdateFrequency = "Periodic", IsOfficial = true,
                DataClassification = DataClassification.Imported
            },
            new DataSource
            {
                Name = "Open-Meteo", Publisher = "Open-Meteo",
                Licence = "CC BY 4.0", LicenceUrl = "https://open-meteo.com/en/license",
                SourceUrl = "https://open-meteo.com/",
                AcquiredAt = DateTimeOffset.UtcNow,
                Coverage = "Port Harcourt grid point (4.8156, 7.0498)",
                QualityNotes = "Gridded forecast/reanalysis, not a local rain gauge.",
                UpdateFrequency = "Hourly", IsOfficial = false,
                DataClassification = DataClassification.Imported
            },
            new DataSource
            {
                Name = "WaterWatch demonstration dataset", Publisher = "WaterWatch",
                Licence = "Synthetic demonstration data — not for operational use",
                SourceUrl = "internal://waterwatch/demo",
                AcquiredAt = DateTimeOffset.UtcNow,
                Coverage = "Port Harcourt and Obio/Akpor demonstration sectors",
                QualityNotes = "All records with this source are synthetic and are labelled SyntheticDemonstration. Service zones and DMAs are analysis sectors, not official utility boundaries.",
                UpdateFrequency = "Generated at seed time", IsOfficial = false,
                DataClassification = DataClassification.SyntheticDemonstration
            }
        };

        foreach (var definition in definitions)
        {
            if (!await db.DataSources.AnyAsync(d => d.Name == definition.Name))
            {
                db.DataSources.Add(definition);
            }
        }
        await db.SaveChangesAsync();
        return await db.DataSources.ToDictionaryAsync(d => d.Name);
    }

    private static async Task EnsureStandardsAsync(DatabaseContext db)
    {
        if (!await db.QualityStandards.AnyAsync(s => s.Code == "NSDWQ-2007"))
        {
            var standard = new QualityStandard
            {
                Code = "NSDWQ-2007", Name = "Nigerian Standard for Drinking Water Quality",
                Authority = "Standards Organisation of Nigeria (SON)", Version = "2007",
                ReferenceUrl = "https://son.gov.ng/", EffectiveFrom = new DateOnly(2007, 1, 1),
                Notes = "Selected operational limits used for demonstration classification. Verify against the current standard before regulatory use.",
                DataClassification = DataClassification.Imported
            };
            ((List<QualityLimit>)standard.Limits).AddRange(new[]
            {
                Limit(ParameterCode.pH, "pH units", 6.5m, 8.5m, "Operational range"),
                Limit(ParameterCode.TurbidityNTU, "NTU", null, 5m, "Appearance/effectiveness"),
                Limit(ParameterCode.TotalDissolvedSolidsMgL, "mg/L", null, 500m, "Palatability"),
                Limit(ParameterCode.NitrateMgL, "mg/L", null, 50m, "As NO3"),
                Limit(ParameterCode.ChlorideMgL, "mg/L", null, 250m, "Palatability"),
                Limit(ParameterCode.IronMgL, "mg/L", null, 0.3m, "Aesthetic/health"),
                Limit(ParameterCode.LeadMgL, "mg/L", null, 0.01m, "Health-based"),
                Limit(ParameterCode.EColiCfu100ml, "CFU/100mL", null, 0m, "Zero detectable"),
                Limit(ParameterCode.TotalColiformCfu100ml, "CFU/100mL", null, 0m, "Zero detectable"),
                Limit(ParameterCode.ElectricalConductivityUScm, "µS/cm", null, 1000m, "Operational")
            });
            db.QualityStandards.Add(standard);
        }
        if (!await db.QualityStandards.AnyAsync(s => s.Code == "WHO-GDWQ-2022"))
        {
            var standard = new QualityStandard
            {
                Code = "WHO-GDWQ-2022", Name = "WHO Guidelines for Drinking-water Quality",
                Authority = "World Health Organization", Version = "4th edition incorporating the first and second addenda (2022)",
                ReferenceUrl = "https://www.who.int/publications/i/item/9789240045064",
                EffectiveFrom = new DateOnly(2022, 3, 21),
                Notes = "Selected guideline values. Some values are aesthetic/operational rather than health-based; see notes.",
                DataClassification = DataClassification.Imported
            };
            ((List<QualityLimit>)standard.Limits).AddRange(new[]
            {
                Limit(ParameterCode.pH, "pH units", 6.5m, 8.5m, "Operational range; not health-based"),
                Limit(ParameterCode.TurbidityNTU, "NTU", null, 5m, "Effectiveness of disinfection; not a health-based value"),
                Limit(ParameterCode.TotalDissolvedSolidsMgL, "mg/L", null, 1000m, "Palatability"),
                Limit(ParameterCode.NitrateMgL, "mg/L", null, 50m, "As NO3"),
                Limit(ParameterCode.ChlorideMgL, "mg/L", null, 250m, "Palatability"),
                Limit(ParameterCode.IronMgL, "mg/L", null, 0.3m, "Aesthetic"),
                Limit(ParameterCode.LeadMgL, "mg/L", null, 0.01m, "Provisional guideline value"),
                Limit(ParameterCode.EColiCfu100ml, "CFU/100mL", null, 0m, "Must not be detectable in any 100 mL sample")
            });
            db.QualityStandards.Add(standard);
        }
        await db.SaveChangesAsync();
    }

    private static QualityLimit Limit(ParameterCode parameter, string unit, decimal? min, decimal? max, string? notes) => new()
    {
        Parameter = parameter, Unit = unit, MinValue = min, MaxValue = max, Notes = notes,
        DataClassification = DataClassification.Imported
    };

    private static async Task EnsureAdministrativeAreasAsync(DatabaseContext db, IGeoJsonService geoJson,
        IWebHostEnvironment environment, Dictionary<string, DataSource> sources, ILogger logger)
    {
        if (await db.AdminAreas.AnyAsync())
        {
            return;
        }
        try
        {
            var path = SourcePath(environment, "nga_admin0.geojson");
            var countryFeature = geoJson.ParseFeatureCollection(await File.ReadAllTextAsync(path)).Features.First();
            var country = new AdminArea
            {
                Code = "NG", Name = "Nigeria", Level = AdminLevel.Country,
                Geometry = countryFeature.Geometry, AreaSqKm = 923768,
                DataSource = sources["OCHA COD-AB Nigeria"], DataClassification = DataClassification.Imported
            };
            db.AdminAreas.Add(country);
            await db.SaveChangesAsync();

            var stateFeature = geoJson.ParseFeatureCollection(await File.ReadAllTextAsync(SourcePath(environment, "rivers_admin1.geojson"))).Features.First();
            var state = new AdminArea
            {
                Code = "NG033", Name = "Rivers State", Level = AdminLevel.State,
                ParentId = country.Id, Geometry = stateFeature.Geometry,
                AreaSqKm = ReadDouble(stateFeature.Properties, "area_sqkm"),
                Population = 7_476_805, PopulationYear = 2022,
                PopulationSource = "NPC 2006 census projection, OCHA COD-PS 2022",
                DataSource = sources["OCHA COD-PS Nigeria"], DataClassification = DataClassification.Imported
            };
            db.AdminAreas.Add(state);
            await db.SaveChangesAsync();

            var lgaFeatures = geoJson.ParseFeatureCollection(await File.ReadAllTextAsync(SourcePath(environment, "port_harcourt_lgas.geojson"))).Features;
            foreach (var feature in lgaFeatures)
            {
                var name = ReadString(feature.Properties, "adm2_name") ?? "Unknown LGA";
                var code = ReadString(feature.Properties, "adm2_pcode") ?? name;
                var lga = new AdminArea
                {
                    Code = code,
                    Name = name == "Obia/Akpor" ? "Obio/Akpor" : name,
                    Alias = name == "Obia/Akpor" ? "Obia/Akpor" : null,
                    Level = AdminLevel.Lga, ParentId = state.Id, Geometry = feature.Geometry,
                    AreaSqKm = ReadDouble(feature.Properties, "area_sqkm"),
                    DataSource = sources["OCHA COD-AB Nigeria"], DataClassification = DataClassification.Imported
                };
                db.AdminAreas.Add(lga);
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded administrative areas from OCHA COD-AB.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not seed administrative boundaries. Check data/sources and the configuration path.");
        }
    }

    private static async Task EnsureOsmLayersAsync(DatabaseContext db, IImportService importService, IGeoJsonService geoJson,
        IWebHostEnvironment environment, Dictionary<string, DataSource> sources, ILogger logger)
    {
        var layers = new (string File, string Key, string Name, string Category, bool Visible)[]
        {
            ("osm_waterways.geojson", "osm-waterways", "Rivers, streams and drainage (OSM)", "Hydrology", true),
            ("osm_water_bodies.geojson", "osm-water-bodies", "Surface water bodies (OSM)", "Hydrology", true),
            ("osm_settlements.geojson", "osm-settlements", "Settlements and neighbourhood labels (OSM)", "Settlements", false),
            ("osm_water_infrastructure.geojson", "osm-water-infrastructure", "Mapped water infrastructure (OSM)", "Infrastructure", true)
        };
        foreach (var (file, key, name, category, visible) in layers)
        {
            if (await db.SpatialLayers.AnyAsync(l => l.Key == key))
            {
                continue;
            }
            try
            {
                var path = SourcePath(environment, file);
                await using var stream = File.OpenRead(path);
                var options = new ImportOptions
                {
                    LayerKey = key, LayerName = name, Category = category,
                    Description = "OpenStreetMap extract with retained tags. See docs/data/waterwatch-spatial-data-inventory.md.",
                    DeclaredSrid = 4326, SourceName = "OpenStreetMap",
                    SourcePublisher = "OpenStreetMap contributors", Licence = "ODbL 1.0",
                    LicenceUrl = "https://www.openstreetmap.org/copyright",
                    SourceUrl = "https://www.openstreetmap.org/",
                    AcquiredAt = DateTimeOffset.Parse("2026-10-23T00:37:05Z"),
                    Commit = true, IsPublic = true
                };
                var result = await importService.ImportAsync(stream, file, options, "system");
                var layer = await db.SpatialLayers.FirstOrDefaultAsync(l => l.Key == key);
                if (layer is not null)
                {
                    layer.IsVisibleByDefault = visible;
                    layer.StyleJson = key switch
                    {
                        "osm-waterways" => JsonSerializer.Serialize(new { color = "#0284c7", weight = 1.6, fillOpacity = 0 }, JsonOptions),
                        "osm-water-bodies" => JsonSerializer.Serialize(new { color = "#0369a1", weight = 1, fillColor = "#7dd3fc", fillOpacity = 0.35 }, JsonOptions),
                        "osm-settlements" => JsonSerializer.Serialize(new { color = "#334155", radius = 4, weight = 1, fillOpacity = 0.7 }, JsonOptions),
                        _ => JsonSerializer.Serialize(new { color = "#0f766e", weight = 2, fillOpacity = 0.7 }, JsonOptions)
                    };
                    await db.SaveChangesAsync();
                }
                logger.LogInformation("Imported OSM layer {Key}: {Accepted} accepted, {Rejected} rejected.", key, result.RowsAccepted, result.RowsRejected);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not import OSM layer {Key}.", key);
            }
        }
    }

    private static async Task EnsureOsmAssetsAsync(DatabaseContext db, IGeoJsonService geoJson,
        IWebHostEnvironment environment, Dictionary<string, DataSource> sources, ILogger logger)
    {
        if (await db.Assets.AnyAsync(a => a.DataClassification == DataClassification.Imported &&
                                           a.SourceReference != null && a.SourceReference.Contains("openstreetmap.org")))
        {
            return;
        }
        try
        {
            var features = geoJson.ParseFeatureCollection(await File.ReadAllTextAsync(SourcePath(environment, "osm_water_infrastructure.geojson"))).Features;
            foreach (var feature in features)
            {
                var manMade = ReadString(feature.Properties, "man_made") ?? "water_tower";
                var osmId = ReadString(feature.Properties, "osm_id") ?? Guid.NewGuid().ToString("N")[..8];
                var geometry = feature.Geometry is Point ? feature.Geometry : feature.Geometry.Centroid;
                var assetType = manMade switch
                {
                    "water_works" => AssetType.TreatmentPlant,
                    "pumping_station" => AssetType.PumpingStation,
                    "water_well" or "borehole" => AssetType.Borehole,
                    "reservoir_covered" => AssetType.Reservoir,
                    _ => AssetType.ElevatedTank
                };
                var code = $"OSM-{assetType.ToString().ToUpperInvariant()[..Math.Min(3, assetType.ToString().Length)]}-{osmId}";
                if (await db.Assets.AnyAsync(a => a.AssetCode == code))
                {
                    continue;
                }
                db.Assets.Add(new Asset
                {
                    AssetCode = code,
                    Name = ReadString(feature.Properties, "name") ?? $"OSM {manMade.Replace('_', ' ')} {osmId}",
                    AssetType = assetType,
                    Description = "Imported from OpenStreetMap. Location and tags are contributed data, not a surveyed asset record.",
                    Geometry = geometry,
                    Status = AssetStatus.Unknown, Condition = ConditionGrade.Unknown,
                    DataSource = sources["OpenStreetMap"],
                    SourceReference = ReadString(feature.Properties, "osm_url"),
                    DataClassification = DataClassification.Imported,
                    IsSensitive = false
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Imported {Count} OSM water infrastructure assets.", features.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not seed OSM assets.");
        }
    }

    private static async Task EnsureDemoDataAsync(DatabaseContext db, IGeoJsonService geoJson,
        Dictionary<string, DataSource> sources, ILogger logger)
    {
        var demo = sources["WaterWatch demonstration dataset"];
        var rng = new Random(20261023);
        var sites = await LoadSitePointsAsync(db, geoJson);
        if (sites.Count < 4)
        {
            logger.LogWarning("Not enough settlement points to build demonstration service zones.");
            return;
        }

        var zones = await EnsureDemoOperationalAreasAsync(db, demo, sites, logger);
        var assets = await EnsureDemoAssetsAsync(db, demo, sites, zones, logger);
        await EnsureDemoConsumptionAsync(db, demo, zones, rng, logger);
        await EnsureDemoQualityAsync(db, demo, sites, rng, logger);
        await EnsureDemoHydrologyAsync(db, demo, rng, logger);
        await EnsureDemoGroundwaterAsync(db, demo, assets, rng, logger);
        await EnsureDemoFloodLayerAsync(db, demo, logger);
        await EnsureDemoNrwAsync(db, demo, rng, logger);
        await EnsureDemoWorkOrdersAsync(db, demo, assets, logger);
        await EnsureDemoTelemetryAsync(db, demo, sites, rng, logger);
    }

    private sealed record SitePoint(string Name, Point Point);

    private static async Task<List<SitePoint>> LoadSitePointsAsync(DatabaseContext db, IGeoJsonService geoJson)
    {
        var features = await db.SpatialFeatures.AsNoTracking()
            .Where(f => f.Layer.Key == "osm-settlements")
            .ToListAsync();
        var lgaGeometries = await db.AdminAreas.AsNoTracking()
            .Where(a => a.Level == AdminLevel.Lga && a.Geometry != null)
            .Select(a => a.Geometry!)
            .ToListAsync();
        var union = UnaryUnionOp.Union(lgaGeometries);
        var points = new List<SitePoint>();
        foreach (var feature in features)
        {
            if (feature.Geometry is not Point point || !union.Contains(point))
            {
                continue;
            }
            var name = feature.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    using var json = JsonDocument.Parse(feature.AttributesJson ?? "{}");
                    if (json.RootElement.TryGetProperty("name", out var nameElement))
                    {
                        name = nameElement.GetString();
                    }
                }
                catch (JsonException) { }
            }
            if (string.IsNullOrWhiteSpace(name) || points.Any(p => p.Name == name))
            {
                continue;
            }
            points.Add(new SitePoint(name!, point));
        }
        return points;
    }

    private static async Task<List<OperationalArea>> EnsureDemoOperationalAreasAsync(DatabaseContext db, DataSource demo, List<SitePoint> sites, ILogger logger)
    {
        var existing = await db.OperationalAreas.Where(o => o.DataClassification == DataClassification.SyntheticDemonstration).ToListAsync();
        if (existing.Count > 0)
        {
            return existing;
        }
        var lgas = await db.AdminAreas.AsNoTracking()
            .Where(a => a.Level == AdminLevel.Lga && a.Geometry != null).ToListAsync();
        var lgaUnion = UnaryUnionOp.Union(lgas.Select(a => a.Geometry!));
        var preferredZones = new[] { "Diobu", "Rumuomasi", "Woji", "Rumuokoro", "Elelenwo", "Borokiri", "Old GRA", "Choba" };
        var zoneSites = preferredZones.Select(name => sites.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Where(s => s is not null).Cast<SitePoint>().DistinctBy(s => s.Name).ToList();
        if (zoneSites.Count < 4)
        {
            zoneSites = sites.Take(8).ToList();
        }
        var zoneGeometries = BuildVoronoiSectors(zoneSites, lgaUnion);
        var created = new List<OperationalArea>();
        var index = 1;
        foreach (var site in zoneSites)
        {
            if (!zoneGeometries.TryGetValue(site.Name, out var geometry) || geometry.IsEmpty)
            {
                continue;
            }
            var zone = new OperationalArea
            {
                Code = $"DEMO-SZ-{index++:D2}", Name = $"Demonstration service zone – {site.Name}",
                AreaType = OperationalAreaType.ServiceZone, Geometry = geometry,
                AreaSqKm = Math.Round(SpatialMath.AreaSquareKilometers(geometry), 3),
                IsOfficial = false, DataSource = demo, DataClassification = DataClassification.SyntheticDemonstration,
                Notes = "Synthetic analysis sector built from a Voronoi partition of OSM settlement labels. NOT an official utility service zone."
            };
            db.OperationalAreas.Add(zone);
            created.Add(zone);
        }
        await db.SaveChangesAsync();

        var preferredDmas = new[] { "Diobu", "Rumuomasi", "Rumuokoro", "Woji" };
        var dmaSites = preferredDmas.Select(name => zoneSites.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Where(s => s is not null).Cast<SitePoint>().ToList();
        if (dmaSites.Count < 2)
        {
            dmaSites = zoneSites.Take(4).ToList();
        }
        var dmaGeometries = BuildVoronoiSectors(dmaSites, lgaUnion);
        index = 1;
        foreach (var site in dmaSites)
        {
            if (!dmaGeometries.TryGetValue(site.Name, out var geometry) || geometry.IsEmpty)
            {
                continue;
            }
            var dma = new OperationalArea
            {
                Code = $"DEMO-DMA-{index++:D2}", Name = $"Demonstration DMA – {site.Name}",
                AreaType = OperationalAreaType.DistrictMeteredArea, Geometry = geometry,
                AreaSqKm = Math.Round(SpatialMath.AreaSquareKilometers(geometry), 3),
                IsOfficial = false, DataSource = demo, DataClassification = DataClassification.SyntheticDemonstration,
                Notes = "Synthetic district metered area for workflow demonstration. NOT an official DMA."
            };
            db.OperationalAreas.Add(dma);
            created.Add(dma);
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created {Zones} synthetic service zones and {Dmas} synthetic DMAs.", created.Count - dmaSites.Count, dmaSites.Count);
        return created;
    }

    private static Dictionary<string, Geometry> BuildVoronoiSectors(IReadOnlyList<SitePoint> sites, Geometry clipTo)
    {
        var result = new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase);
        if (sites.Count < 3)
        {
            return result;
        }
        var builder = new VoronoiDiagramBuilder { Tolerance = 1e-7 };
        builder.SetSites(sites.Select(s => s.Point.Coordinate).ToList());
        var envelope = new Envelope(clipTo.EnvelopeInternal);
        envelope.ExpandBy(0.05);
        builder.ClipEnvelope = envelope;
        var diagram = builder.GetDiagram(SpatialMath.Wgs84Factory);
        var cells = diagram is GeometryCollection collection
            ? collection.Geometries.Where(g => !g.IsEmpty).ToList()
            : new List<Geometry> { diagram };
        foreach (var site in sites)
        {
            var cell = cells.FirstOrDefault(c => c.Contains(site.Point)) ??
                       cells.OrderBy(c => SpatialMath.ToUtm32(c).Distance(SpatialMath.ToUtm32(site.Point))).FirstOrDefault();
            if (cell is null)
            {
                continue;
            }
            var clipped = cell.Intersection(clipTo);
            if (!clipped.IsEmpty)
            {
                clipped.SRID = SpatialMath.Wgs84;
                clipped.GeometryChanged();
                result[site.Name] = clipped;
            }
        }
        return result;
    }

    private static async Task<List<Asset>> EnsureDemoAssetsAsync(DatabaseContext db, DataSource demo, List<SitePoint> sites,
        List<OperationalArea> areas, ILogger logger)
    {
        var existing = await db.Assets.Where(a => a.DataClassification == DataClassification.SyntheticDemonstration).ToListAsync();
        if (existing.Count > 0)
        {
            return existing;
        }
        var zones = areas.Where(a => a.AreaType == OperationalAreaType.ServiceZone).ToList();
        var dmas = areas.Where(a => a.AreaType == OperationalAreaType.DistrictMeteredArea).ToList();
        var definitions = new (string Name, AssetType Type, string Site, bool Sensitive)[]
        {
            ("Demonstration treatment plant – Diobu", AssetType.TreatmentPlant, "Diobu", false),
            ("Demonstration reservoir – Rumuomasi", AssetType.Reservoir, "Rumuomasi", false),
            ("Demonstration pumping station – Woji", AssetType.PumpingStation, "Woji", true),
            ("Demonstration borehole – Choba", AssetType.Borehole, "Choba", false),
            ("Demonstration borehole – Elelenwo", AssetType.Borehole, "Elelenwo", false),
            ("Demonstration borehole – Rumuokoro", AssetType.Borehole, "Rumuokoro", false),
            ("Demonstration valve – Old GRA", AssetType.Valve, "Old GRA", false),
            ("Demonstration hydrant – Borokiri", AssetType.Hydrant, "Borokiri", false),
            ("Demonstration pressure point – Woji", AssetType.PressureMonitoringPoint, "Woji", false),
            ("Demonstration elevated tank – Rumuomasi", AssetType.ElevatedTank, "Rumuomasi", false)
        };
        var created = new List<Asset>();
        var index = 1;
        foreach (var (name, type, siteName, sensitive) in definitions)
        {
            var site = sites.FirstOrDefault(s => s.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase)) ?? sites[index % sites.Count];
            var zone = zones.FirstOrDefault(z => z.Geometry != null && z.Geometry.Contains(site.Point));
            var dma = dmas.FirstOrDefault(d => d.Geometry != null && d.Geometry.Contains(site.Point));
            var asset = new Asset
            {
                AssetCode = $"DEMO-ASSET-{index++:D3}", Name = name + " (synthetic)",
                AssetType = type, Description = "Synthetic demonstration asset. Not a real Port Harcourt utility asset.",
                Geometry = site.Point, Status = AssetStatus.Operational,
                Condition = index % 3 == 0 ? ConditionGrade.Fair : ConditionGrade.Good,
                AdminAreaId = await FindLgaIdAsync(db, site.Point), OperationalAreaId = zone?.Id,
                Owner = "WaterWatch demonstration", Operator = "WaterWatch demonstration",
                InstalledOn = new DateOnly(2015 + (index % 8), 1 + (index % 12), 15),
                CapacityM3PerDay = type switch
                {
                    AssetType.TreatmentPlant => 15000, AssetType.Reservoir => 5000,
                    AssetType.PumpingStation => 2400, _ => null
                },
                IsSensitive = sensitive, DataSource = demo,
                DataClassification = DataClassification.SyntheticDemonstration
            };
            db.Assets.Add(asset);
            created.Add(asset);
        }
        await db.SaveChangesAsync();

        // Schematic network — explicitly synthetic connectivity.
        var networkAssets = created.Take(6).ToList();
        var nodeTypes = new[] { NetworkNodeType.Treatment, NetworkNodeType.Pump, NetworkNodeType.Tank,
            NetworkNodeType.Junction, NetworkNodeType.Junction, NetworkNodeType.Demand };
        var nodes = new List<NetworkNode>();
        for (var i = 0; i < networkAssets.Count; i++)
        {
            var node = new NetworkNode
            {
                Code = $"DEMO-NODE-{i + 1:D2}", Name = $"{networkAssets[i].Name} node",
                NodeType = nodeTypes[i], AssetId = networkAssets[i].Id,
                Geometry = networkAssets[i].Geometry?.Centroid,
                IsVerified = true, DataClassification = DataClassification.SyntheticDemonstration,
                Notes = "Schematic demonstration connectivity; not a surveyed network."
            };
            db.NetworkNodes.Add(node);
            nodes.Add(node);
        }
        await db.SaveChangesAsync();
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            var from = nodes[i].Geometry;
            var to = nodes[i + 1].Geometry;
            if (from is null || to is null)
            {
                continue;
            }
            db.NetworkEdges.Add(new NetworkEdge
            {
                Code = $"DEMO-EDGE-{i + 1:D2}", Name = $"Demonstration schematic main {i + 1}",
                FromNodeId = nodes[i].Id, ToNodeId = nodes[i + 1].Id,
                Geometry = SpatialMath.Wgs84Factory.CreateLineString(new[] { from.Coordinate, to.Coordinate }),
                DiameterMm = 200 + i * 50, Material = "Unknown (synthetic)", IsVerified = true,
                LengthM = (decimal)Math.Round(SpatialMath.LengthMeters(SpatialMath.Wgs84Factory.CreateLineString(new[] { from.Coordinate, to.Coordinate })), 1),
                DataClassification = DataClassification.SyntheticDemonstration
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created {Count} synthetic demonstration assets and a schematic network.", created.Count);
        return created;
    }

    private static async Task<Guid?> FindLgaIdAsync(DatabaseContext db, Point point) =>
        (await db.AdminAreas.AsNoTracking().Where(a => a.Level == AdminLevel.Lga && a.Geometry != null &&
            a.Geometry.Contains(point)).Select(a => (Guid?)a.Id).FirstOrDefaultAsync());

    private static async Task EnsureDemoConsumptionAsync(DatabaseContext db, DataSource demo,
        List<OperationalArea> areas, Random rng, ILogger logger)
    {
        if (await db.ConsumerAccounts.AnyAsync())
        {
            return;
        }
        var zones = areas.Where(a => a.AreaType == OperationalAreaType.ServiceZone).ToList();
        var dmas = areas.Where(a => a.AreaType == OperationalAreaType.DistrictMeteredArea).ToList();
        if (zones.Count == 0)
        {
            return;
        }
        var categories = new[] { ConsumerCategory.Domestic, ConsumerCategory.Domestic, ConsumerCategory.Domestic,
            ConsumerCategory.Commercial, ConsumerCategory.Institutional, ConsumerCategory.Industrial };
        var startMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-17);
        for (var i = 1; i <= 80; i++)
        {
            var zone = zones[rng.Next(zones.Count)];
            var zonePoint = zone.Geometry?.InteriorPoint ?? SpatialMath.Point(7.0, 4.8);
            var dma = dmas.FirstOrDefault(d => d.Geometry != null && d.Geometry.Contains(zonePoint)) ?? dmas.FirstOrDefault();
            var category = categories[rng.Next(categories.Length)];
            var account = new ConsumerAccount
            {
                AccountNumber = $"DEMO-C-{i:D4}", Name = $"Demonstration consumer {i:D4}", Category = category,
                AdminAreaId = zonePoint is null ? null : await FindLgaIdAsync(db, zonePoint),
                ServiceZoneId = zone.Id, DmaId = dma?.Id,
                HouseholdSize = category == ConsumerCategory.Domestic ? rng.Next(2, 7) : null,
                IsSensitive = false, Notes = "Synthetic demonstration account. Not a real consumer.",
                DataClassification = DataClassification.SyntheticDemonstration
            };
            var meter = new Meter
            {
                SerialNumber = $"DEMO-M-{i:D4}", ConsumerAccount = account, InstalledOn = startMonth.AddMonths(-6),
                Status = MeterStatus.Active, Multiplier = 1m, Unit = "m3",
                Manufacturer = "Demonstration", Model = "Synthetic", RolloverValueM3 = 999999m,
                DataClassification = DataClassification.SyntheticDemonstration
            };
            var baseConsumption = category switch
            {
                ConsumerCategory.Industrial => 180m + rng.Next(40, 180),
                ConsumerCategory.Commercial => 45m + rng.Next(10, 50),
                ConsumerCategory.Institutional => 30m + rng.Next(10, 40),
                _ => 8m + rng.Next(3, 14)
            };
            var cumulative = i == 1 ? 999850m : 1000m + rng.Next(100, 2000);
            for (var m = 0; m < 18; m++)
            {
                var date = startMonth.AddMonths(m);
                var type = ReadingType.Observed;
                var value = cumulative + baseConsumption * (0.75m + (decimal)rng.NextDouble() * 0.5m);
                if (i == 1 && m == 8)
                {
                    type = ReadingType.Rollover;
                    value = baseConsumption * (0.75m + (decimal)rng.NextDouble() * 0.5m);
                }
                else if (rng.Next(100) < 8)
                {
                    type = ReadingType.Estimated;
                }
                meter.Readings.Add(new MeterReading
                {
                    ReadingDate = date, ReadingValueM3 = Math.Round(value, 1), ReadingType = type,
                    Notes = type == ReadingType.Estimated ? "Synthetic estimated reading." : "Synthetic demonstration reading.",
                    DataClassification = DataClassification.SyntheticDemonstration
                });
                cumulative = value;
            }
            db.ConsumerAccounts.Add(account);
            db.Meters.Add(meter);
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created 80 synthetic consumer accounts with 18 months of readings.");
    }

    private static async Task EnsureDemoQualityAsync(DatabaseContext db, DataSource demo, List<SitePoint> sites, Random rng, ILogger logger)
    {
        if (await db.SamplingStations.AnyAsync(s => s.DataClassification == DataClassification.SyntheticDemonstration))
        {
            return;
        }
        var standard = await db.QualityStandards.AsNoTracking().Include(s => s.Limits).FirstAsync(s => s.Code == "NSDWQ-2007");
        var limitMap = standard.Limits.ToDictionary(l => l.Parameter);
        var campaign = new SamplingCampaign
        {
            Code = "DEMO-QC-2026", Name = "Demonstration water quality campaign", Status = SamplingCampaignStatus.Active,
            StartedOn = new DateOnly(DateTime.UtcNow.Year, 1, 1), Purpose = "Exercise classification, trends and exceedance workflows.",
            LaboratoryName = "Demonstration laboratory (synthetic)", LaboratoryAccredited = false,
            DataClassification = DataClassification.SyntheticDemonstration
        };
        db.SamplingCampaigns.Add(campaign);
        var selected = new[] { "Diobu", "Rumuomasi", "Woji", "Rumuokoro", "Elelenwo", "Borokiri" };
        var stations = new List<SamplingStation>();
        var index = 1;
        foreach (var name in selected)
        {
            var site = sites.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? sites[index % sites.Count];
            var station = new SamplingStation
            {
                Code = $"DEMO-SS-{index++:D2}", Name = $"Demonstration sampling station – {name} (synthetic)",
                WaterSourceType = index % 2 == 0 ? WaterSourceType.DistributedSupply : WaterSourceType.Groundwater,
                Geometry = site.Point, AdminAreaId = await FindLgaIdAsync(db, site.Point),
                Description = "Synthetic sampling station; coordinates follow an OSM settlement label.",
                IsActive = true, DataSource = demo, DataClassification = DataClassification.SyntheticDemonstration
            };
            db.SamplingStations.Add(station);
            stations.Add(station);
        }
        await db.SaveChangesAsync();

        var parameters = new (ParameterCode Parameter, Func<decimal> Value)[]
        {
            (ParameterCode.pH, () => 6.0m + (decimal)rng.NextDouble() * 2.4m),
            (ParameterCode.TurbidityNTU, () => Math.Round(0.5m + (decimal)rng.NextDouble() * 6.5m, 2)),
            (ParameterCode.TotalDissolvedSolidsMgL, () => 80m + (decimal)rng.NextDouble() * 620m),
            (ParameterCode.NitrateMgL, () => Math.Round(3m + (decimal)rng.NextDouble() * 57m, 2)),
            (ParameterCode.ChlorideMgL, () => Math.Round(15m + (decimal)rng.NextDouble() * 285m, 1)),
            (ParameterCode.IronMgL, () => Math.Round(0.02m + (decimal)rng.NextDouble() * 0.8m, 3)),
            (ParameterCode.LeadMgL, () => Math.Round(0.0005m + (decimal)rng.NextDouble() * 0.018m, 4)),
            (ParameterCode.EColiCfu100ml, () => rng.Next(0, 8)),
            (ParameterCode.TotalColiformCfu100ml, () => rng.Next(0, 25)),
            (ParameterCode.ElectricalConductivityUScm, () => 90m + (decimal)rng.NextDouble() * 1110m)
        };
        var sampleIndex = 1;
        foreach (var station in stations)
        {
            for (var m = 0; m < 6; m++)
            {
                var sampledAt = Lagos(DateTime.UtcNow.AddMonths(-5 + m), 9);
                var sample = new WaterSample
                {
                    SampleCode = $"DEMO-S-{sampleIndex++:D4}", StationId = station.Id, CampaignId = campaign.Id,
                    SampledAt = sampledAt, SampledBy = "Demonstration sampler", ReceivedAt = sampledAt.AddHours(4),
                    Notes = "Synthetic demonstration sample.", DataClassification = DataClassification.SyntheticDemonstration
                };
                foreach (var (parameter, valueFactory) in parameters)
                {
                    if (!limitMap.TryGetValue(parameter, out var limit))
                    {
                        continue;
                    }
                    var value = Math.Round(valueFactory(), 4);
                    var (state, note) = QualityService.Assess(value, limit.Unit, limit);
                    sample.Results.Add(new SampleResult
                    {
                        Parameter = parameter, Value = value, Unit = limit.Unit,
                        DetectionLimit = parameter is ParameterCode.LeadMgL ? 0.001m : null,
                        Method = "Demonstration laboratory method",
                        State = state, AppliedLimitId = limit.Id, StandardCode = standard.Code,
                        AssessmentNote = note, AssessedAt = DateTimeOffset.UtcNow,
                        DataClassification = DataClassification.SyntheticDemonstration
                    });
                }
                db.WaterSamples.Add(sample);
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created {Samples} synthetic water samples with classified results.", sampleIndex - 1);
    }

    private static async Task EnsureDemoHydrologyAsync(DatabaseContext db, DataSource demo, Random rng, ILogger logger)
    {
        if (await db.HydrologyStations.AnyAsync(s => s.DataClassification == DataClassification.SyntheticDemonstration))
        {
            return;
        }
        var waterways = await db.SpatialFeatures.AsNoTracking()
            .Where(f => f.Layer.Key == "osm-waterways" && f.Geometry != null)
            .Take(2).ToListAsync();
        var names = new[] { "Woji Creek", "Rumuokoro drainage corridor" };
        for (var i = 0; i < Math.Min(2, Math.Max(1, waterways.Count)); i++)
        {
            var geometry = waterways.Count == 0 ? SpatialMath.Point(7.02 + i * 0.02, 4.82 + i * 0.02) : waterways[i].Geometry.Centroid;
            var station = new HydrologyStation
            {
                Code = $"DEMO-HYD-{i + 1:D2}", Name = $"Demonstration water-level station – {names[i]} (synthetic)",
                StationType = HydrologyStationType.WaterLevel, Geometry = geometry as Point ?? SpatialMath.Point(geometry.X, geometry.Y),
                WaterwayName = names[i], Datum = "Local demonstration datum", IsActive = true, DataSource = demo,
                Notes = "Synthetic water-level readings; not a physical gauge.", DataClassification = DataClassification.SyntheticDemonstration
            };
            db.HydrologyStations.Add(station);
            var start = Lagos(DateTime.UtcNow.AddMonths(-11), 0);
            for (var m = 0; m < 12; m++)
            {
                db.HydrologyObservations.Add(new HydrologyObservation
                {
                    Station = station, ObservedAt = start.AddMonths(m).AddHours(9),
                    Parameter = HydrologyParameter.WaterLevelM, Unit = "m",
                    Value = Math.Round(1.4m + (decimal)rng.NextDouble() * 1.2m + m * 0.015m, 3),
                    QualityFlag = QualityFlag.Provisional, DataSource = demo,
                    Notes = "Synthetic demonstration level.", DataClassification = DataClassification.SyntheticDemonstration
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created synthetic hydrology stations and observations.");
    }

    private static async Task EnsureDemoGroundwaterAsync(DatabaseContext db, DataSource demo, List<Asset> assets, Random rng, ILogger logger)
    {
        var boreholes = assets.Where(a => a.AssetType == AssetType.Borehole).Take(3).ToList();
        if (boreholes.Count == 0 || await db.BoreholeDetails.AnyAsync())
        {
            return;
        }
        foreach (var borehole in boreholes)
        {
            db.BoreholeDetails.Add(new BoreholeDetail
            {
                AssetId = borehole.Id, BoreholeDepthM = 45m, ScreenTopM = 18m, ScreenBottomM = 40m,
                AquiferName = "Coastal plain sands (inferred)", AquiferClassification = "Sedimentary",
                AquiferClassificationInferred = true, StaticWaterLevelM = 6.2m, DynamicWaterLevelM = 7.4m,
                YieldM3PerHour = 2.6m, DrilledOn = new DateOnly(2018, 6, 1),
                Driller = "Demonstration driller", DataClassification = DataClassification.SyntheticDemonstration
            });
            for (var m = 0; m < 18; m++)
            {
                db.GroundwaterReadings.Add(new GroundwaterReading
                {
                    BoreholeAssetId = borehole.Id,
                    ObservedAt = Lagos(DateTime.UtcNow.AddMonths(-17 + m), 8),
                    WaterLevelM = Math.Round(5.8m + (decimal)rng.NextDouble() * 1.8m - m * 0.01m, 2),
                    AbstractionM3 = Math.Round(80m + (decimal)rng.NextDouble() * 420m, 1),
                    Notes = "Synthetic demonstration groundwater record.", DataSource = demo,
                    DataClassification = DataClassification.SyntheticDemonstration
                });
            }
            db.PumpingTests.Add(new PumpingTest
            {
                BoreholeAssetId = borehole.Id, TestedAt = DateTimeOffset.UtcNow.AddMonths(-8),
                DurationMinutes = 240, PumpingRateM3PerHour = 2.4m, DrawdownM = 1.3m, RecoveryMinutes = 90,
                ConductedBy = "Demonstration hydrogeologist", ResultJson = "{\"method\":\"constant discharge (synthetic)\"}",
                DataClassification = DataClassification.SyntheticDemonstration
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created synthetic groundwater details, readings and pumping tests.");
    }

    private static async Task EnsureDemoFloodLayerAsync(DatabaseContext db, DataSource demo, ILogger logger)
    {
        if (await db.FloodLayers.AnyAsync(f => f.DataClassification == DataClassification.SyntheticDemonstration))
        {
            return;
        }
        var waterways = await db.SpatialFeatures.AsNoTracking()
            .Where(f => f.Layer.Key == "osm-waterways" && f.Geometry != null)
            .Select(f => f.Geometry)
            .ToListAsync();
        var lgas = await db.AdminAreas.AsNoTracking()
            .Where(a => a.Level == AdminLevel.Lga && a.Geometry != null)
            .Select(a => a.Geometry!)
            .ToListAsync();
        if (waterways.Count == 0 || lgas.Count == 0)
        {
            return;
        }
        var lgaUnion = UnaryUnionOp.Union(lgas);
        var buffers = waterways.Select(g => SpatialMath.ToUtm32(g!).Buffer(120)).ToList();
        var union = UnaryUnionOp.Union(buffers);
        var clipped = SpatialMath.ToWgs84(union).Intersection(lgaUnion);
        if (clipped.IsEmpty)
        {
            return;
        }
        clipped.SRID = SpatialMath.Wgs84;
        var layer = new FloodLayer
        {
            Name = "Synthetic demonstration flood susceptibility (120 m waterway buffer)",
            LayerType = FloodLayerType.ModelledSusceptibility,
            Description = "A demonstration-only susceptibility layer generated by buffering mapped OSM waterway centrelines. It is not observed flooding and not a forecast.",
            Geometry = clipped, IsOfficial = false, DataSource = demo,
            Assumptions = "120 m planar buffer of OSM waterway centrelines; no DEM, rainfall, tide, drainage capacity or defence modelling.",
            Uncertainty = "High. OSM waterway coverage is incomplete; a mapped centreline is not a surveyed channel or floodplain.",
            ValidationStatus = "Synthetic demonstration — unvalidated",
            ModelVersion = "demo-waterway-buffer-1.0",
            DataClassification = DataClassification.SyntheticDemonstration
        };
        db.FloodLayers.Add(layer);
        await db.SaveChangesAsync();
        logger.LogInformation("Created a labelled synthetic flood susceptibility layer (not a forecast).");
    }

    private static async Task EnsureDemoNrwAsync(DatabaseContext db, DataSource demo, Random rng, ILogger logger)
    {
        if (await db.DmaWaterBalances.AnyAsync(b => b.DataClassification == DataClassification.SyntheticDemonstration))
        {
            return;
        }
        var dmas = await db.OperationalAreas.Where(a => a.AreaType == OperationalAreaType.DistrictMeteredArea)
            .Where(a => a.DataClassification == DataClassification.SyntheticDemonstration).ToListAsync();
        var lastMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
        var index = 0;
        foreach (var dma in dmas)
        {
            index++;
            for (var m = 11; m >= 0; m--)
            {
                var periodEnd = lastMonth.AddMonths(-m);
                var input = 80000m + rng.Next(20000, 120000);
                var billed = input * (0.62m + (decimal)rng.NextDouble() * 0.18m);
                var nrw = input - billed;
                decimal? apparent = null;
                decimal? real = null;
                if (index % 2 == 0)
                {
                    apparent = Math.Round(nrw * 0.32m, 1);
                    real = Math.Round(nrw * 0.68m, 1);
                }
                var balance = new DmaWaterBalance
                {
                    DmaId = dma.Id, PeriodStart = periodEnd.AddMonths(-1), PeriodEnd = periodEnd,
                    InputVolumeM3 = Math.Round(input, 1), BilledVolumeM3 = Math.Round(billed, 1),
                    ApparentLossesM3 = apparent, RealLossesM3 = real,
                    MinimumNightFlowM3PerHour = Math.Round((decimal)rng.NextDouble() * 4m, 2),
                    Method = "Synthetic demonstration DMA water balance",
                    UncertaintyNotes = index % 2 == 0 ? null : "Apparent/real loss split not evidenced; NRW remains unallocated.",
                    DataSource = demo, DataClassification = DataClassification.SyntheticDemonstration
                };
                db.DmaWaterBalances.Add(balance);
            }
            for (var l = 0; l < 2; l++)
            {
                var point = dma.Geometry?.InteriorPoint ?? SpatialMath.Point(7.0, 4.8);
                db.LeakIncidents.Add(new LeakIncident
                {
                    Reference = $"DEMO-LEAK-{index:D2}-{l + 1}", ReportedAt = DateTimeOffset.UtcNow.AddDays(-rng.Next(1, 40)),
                    DmaId = dma.Id, Geometry = point, Description = $"Synthetic leak incident {l + 1} in {dma.Name}.",
                    Severity = l == 0 ? Severity.High : Severity.Medium,
                    Status = l == 0 ? LeakStatus.Assigned : LeakStatus.Reported,
                    EstimatedLossM3PerHour = Math.Round(1m + (decimal)rng.NextDouble() * 8m, 2),
                    EstimatedLossIsEstimate = true, DataClassification = DataClassification.SyntheticDemonstration
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created synthetic DMA water balances and leak incidents.");
    }

    private static async Task EnsureDemoWorkOrdersAsync(DatabaseContext db, DataSource demo, List<Asset> assets, ILogger logger)
    {
        if (await db.WorkOrders.AnyAsync(w => w.DataClassification == DataClassification.SyntheticDemonstration))
        {
            return;
        }
        var statuses = new[] { WorkOrderStatus.Reported, WorkOrderStatus.Assigned, WorkOrderStatus.InProgress,
            WorkOrderStatus.Resolved, WorkOrderStatus.Verified, WorkOrderStatus.Closed };
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < statuses.Length; i++)
        {
            var asset = assets.Count > 0 ? assets[i % assets.Count] : null;
            var order = new WorkOrder
            {
                Reference = $"DEMO-WO-{i + 1:D3}", Title = $"Demonstration {statuses[i]} work order",
                Description = "Synthetic field-operations record used to exercise the workflow.",
                Type = i % 3 == 0 ? WorkOrderType.LeakRepair : i % 3 == 1 ? WorkOrderType.Inspection : WorkOrderType.Maintenance,
                Priority = i == 0 ? Priority.Critical : i < 3 ? Priority.High : Priority.Medium,
                Status = statuses[i], AssetId = asset?.Id, Geometry = asset?.Geometry?.Centroid,
                ReportedBy = "reviewer@waterwatch.local", AssignedTo = "inspector@waterwatch.local",
                DueAt = now.AddDays(7 - i), DataClassification = DataClassification.SyntheticDemonstration,
                CreatedAt = now.AddDays(-10 + i)
            };
            var sequence = new[] { WorkOrderStatus.Reported, WorkOrderStatus.Triaged, WorkOrderStatus.Assigned,
                WorkOrderStatus.InProgress, WorkOrderStatus.Resolved, WorkOrderStatus.Verified, WorkOrderStatus.Closed };
            WorkOrderStatus? previous = null;
            foreach (var status in sequence.TakeWhile(s => (int)s <= (int)statuses[i]))
            {
                var changedBy = status == WorkOrderStatus.Resolved ? "engineer@waterwatch.local" :
                                status is WorkOrderStatus.Verified or WorkOrderStatus.Closed ? "reviewer@waterwatch.local" :
                                "inspector@waterwatch.local";
                order.Events.Add(new WorkOrderEvent
                {
                    FromStatus = previous, ToStatus = status, ChangedBy = changedBy,
                    ChangedAt = now.AddDays(-8 + i).AddHours((int)status),
                    Notes = status == WorkOrderStatus.Resolved ? "Demonstration resolution." :
                            status == WorkOrderStatus.Verified ? "Demonstration verification." : null,
                    DataClassification = DataClassification.SyntheticDemonstration
                });
                previous = status;
            }
            if (statuses[i] >= WorkOrderStatus.Resolved)
            {
                order.ResolvedAt = now.AddDays(-1);
                order.ResolvedBy = "engineer@waterwatch.local";
                order.ResolutionNotes = "Synthetic resolution notes.";
            }
            if (statuses[i] >= WorkOrderStatus.Verified)
            {
                order.VerifiedAt = now.AddHours(-2);
                order.VerifiedBy = "reviewer@waterwatch.local";
                order.VerificationNotes = "Synthetic verification notes.";
            }
            if (statuses[i] == WorkOrderStatus.Closed)
            {
                order.ClosedAt = now.AddHours(-1);
                order.ClosedBy = "reviewer@waterwatch.local";
            }
            db.WorkOrders.Add(order);
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created synthetic work orders across the workflow.");
    }

    private static async Task EnsureDemoTelemetryAsync(DatabaseContext db, DataSource demo, List<SitePoint> sites, Random rng, ILogger logger)
    {
        if (await db.Devices.AnyAsync(d => d.IsDemoDevice))
        {
            return;
        }
        var definitions = new (string Code, string Name, DeviceType Type, string Site, string Parameter, string Unit)[]
        {
            ("DEMO-PRESSURE", "Demonstration pressure sensor – Woji", DeviceType.Pressure, "Woji", "pressure_bar", "bar"),
            ("DEMO-RESERVOIR", "Demonstration reservoir level – Rumuomasi", DeviceType.ReservoirLevel, "Rumuomasi", "level_m", "m"),
            ("DEMO-FLOW", "Demonstration flow sensor – Diobu", DeviceType.Flow, "Diobu", "flow_m3s", "m3/s"),
            ("DEMO-TURBIDITY", "Demonstration turbidity sensor – Borokiri", DeviceType.WaterQuality, "Borokiri", "turbidity_ntu", "NTU")
        };
        var devices = new List<Device>();
        foreach (var (code, name, type, siteName, parameter, unit) in definitions)
        {
            var site = sites.FirstOrDefault(s => s.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase)) ?? sites[0];
            var device = new Device
            {
                DeviceCode = code, Name = name, DeviceType = type, Geometry = site.Point,
                Status = DeviceStatus.Active, InstalledOn = new DateOnly(2025, 1, 15),
                OfflineThresholdMinutes = 180, IsDemoDevice = true,
                DataClassification = DataClassification.SyntheticDemonstration,
                FirmwareVersion = "demo-1.0"
            };
            db.Devices.Add(device);
            devices.Add(device);
        }
        await db.SaveChangesAsync();

        var rules = new[]
        {
            new AlertRule { Name = "Demonstration low pressure", DeviceType = DeviceType.Pressure, Parameter = "pressure_bar", Operator = AlertOperator.LessThan, Threshold = 1.5m, Severity = Severity.High, Description = "Synthetic demonstration rule; not an operational pressure standard." },
            new AlertRule { Name = "Demonstration high turbidity", DeviceType = DeviceType.WaterQuality, Parameter = "turbidity_ntu", Operator = AlertOperator.GreaterThan, Threshold = 5m, Severity = Severity.High, Description = "Synthetic demonstration rule." },
            new AlertRule { Name = "Demonstration reservoir high level", DeviceType = DeviceType.ReservoirLevel, Parameter = "level_m", Operator = AlertOperator.GreaterThan, Threshold = 4.5m, Severity = Severity.Medium, Description = "Synthetic demonstration rule." },
            new AlertRule { Name = "Demonstration low flow", DeviceType = DeviceType.Flow, Parameter = "flow_m3s", Operator = AlertOperator.LessThan, Threshold = 0.05m, Severity = Severity.Medium, Description = "Synthetic demonstration rule." }
        };
        db.AlertRules.AddRange(rules);
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        Alert? seededAlert = null;
        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var (parameter, unit) = i switch
            {
                0 => ("pressure_bar", "bar"),
                1 => ("level_m", "m"),
                2 => ("flow_m3s", "m3/s"),
                _ => ("turbidity_ntu", "NTU")
            };
            decimal lastValue = 0;
            for (var n = 0; n < 12; n++)
            {
                var value = i switch
                {
                    0 => Math.Round(1.8m + (decimal)rng.NextDouble() * 1.2m, 3),
                    1 => Math.Round(3.2m + (decimal)rng.NextDouble() * 1.4m, 3),
                    2 => Math.Round(0.12m + (decimal)rng.NextDouble() * 0.25m, 3),
                    _ => Math.Round(1.5m + (decimal)rng.NextDouble() * 5m, 3)
                };
                if (i == 0 && n == 9)
                {
                    value = 1.2m;
                    if (seededAlert is null)
                    {
                        seededAlert = new Alert
                        {
                            AlertRule = rules[0], Device = device, TriggeredAt = now.AddMinutes(-6),
                            Value = value, Message = "Synthetic low-pressure demonstration alert.",
                            Status = AlertStatus.Open, DataClassification = DataClassification.SyntheticDemonstration
                        };
                    }
                }
                lastValue = value;
                db.TelemetryReadings.Add(new TelemetryReading
                {
                    DeviceId = device.Id, ObservedAt = now.AddMinutes(-n * 5), Parameter = parameter, Value = value,
                    Unit = unit, Quality = TelemetryQuality.SyntheticDemonstration,
                    DataClassification = DataClassification.SyntheticDemonstration,
                    ReceivedAt = now.AddMinutes(-n * 5), RawPayloadJson = "{\"source\":\"seed\"}"
                });
            }
            device.LastSeenAt = now.AddMinutes(-1);
            device.Status = DeviceStatus.Active;
        }
        if (seededAlert is not null)
        {
            db.Alerts.Add(seededAlert);
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created synthetic telemetry devices, readings and alert rules. All are labelled synthetic.");
    }

    private static DateTimeOffset Lagos(DateTime date, int hour) =>
        new(new DateTime(date.Year, date.Month, date.Day, hour, 0, 0, DateTimeKind.Unspecified), TimeSpan.FromHours(1));

    private static string SourcePath(IWebHostEnvironment environment, string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "..", "data", "sources", fileName),
            Path.Combine(environment.ContentRootPath, "data", "sources", fileName),
            Path.Combine(environment.ContentRootPath, "App_Data", "sources", fileName),
            Path.Combine(AppContext.BaseDirectory, "App_Data", "sources", fileName)
        };
        return candidates.FirstOrDefault(File.Exists)
               ?? throw new FileNotFoundException($"Spatial source '{fileName}' was not found. Checked: {string.Join(", ", candidates)}");
    }

    private static string? ReadString(Dictionary<string, object?> properties, string key) =>
        properties.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static double? ReadDouble(Dictionary<string, object?> properties, string key) =>
        properties.TryGetValue(key, out var value) && double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : null;
}
