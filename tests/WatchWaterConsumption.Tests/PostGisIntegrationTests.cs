using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Services;

namespace WatchWaterConsumption.Tests;

[TestFixture]
public class PostGisIntegrationTests
{
    private string? _connectionString;

    [OneTimeSetUp]
    public void SetUp()
    {
        _connectionString = Environment.GetEnvironmentVariable("WATERWATCH_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            Assert.Ignore("Set WATERWATCH_TEST_CONNECTION to a disposable PostgreSQL database with PostGIS to run this integration test.");
        }
    }

    private DatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseNpgsql(_connectionString, npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new DatabaseContext(options);
    }

    [Test]
    public async Task PostGisGeometryIndexesAndSpatialPredicatesWork()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var polygon = SpatialMath.Wgs84Factory.CreatePolygon(new[]
        {
            new Coordinate(6.99, 4.79), new Coordinate(7.02, 4.79),
            new Coordinate(7.02, 4.82), new Coordinate(6.99, 4.82),
            new Coordinate(6.99, 4.79)
        });
        polygon.SRID = SpatialMath.Wgs84;
        var area = new AdminArea
        {
            Code = "TEST-LGA-" + suffix, Name = "Integration LGA", Level = AdminLevel.Lga,
            Geometry = polygon, DataClassification = DataClassification.Imported
        };
        var asset = new Asset
        {
            AssetCode = "TEST-GIS-" + suffix, Name = "Integration asset",
            AssetType = AssetType.Valve, Geometry = SpatialMath.Point(7.005, 4.805),
            DataClassification = DataClassification.Observed
        };
        db.AdminAreas.Add(area);
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        (await db.AdminAreas.CountAsync(a => a.Geometry != null && a.Geometry.Contains(asset.Geometry!)))
            .Should().Be(1);
        (await db.Assets.CountAsync(a => a.Geometry != null && a.Geometry.Intersects(polygon)))
            .Should().Be(1);

        var buffer = SpatialMath.BufferGeometryMeters(asset.Geometry!, 150);
        SpatialMath.AreaSquareKilometers(buffer).Should().BeGreaterThan(0.05);
        (await db.Assets.CountAsync(a => a.Geometry != null && a.Geometry.Intersects(buffer)))
            .Should().Be(1);

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT count(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Assets' AND indexdef ILIKE '%gist%'";
        if (command.Connection?.State != ConnectionState.Open)
        {
            await command.Connection!.OpenAsync();
        }
        var gistIndexes = Convert.ToInt64(await command.ExecuteScalarAsync());
        gistIndexes.Should().BeGreaterThan(0);

        db.Assets.Remove(asset);
        db.AdminAreas.Remove(area);
        await db.SaveChangesAsync();
    }
}
