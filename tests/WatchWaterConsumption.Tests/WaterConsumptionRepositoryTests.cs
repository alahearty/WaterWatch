using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using NUnit.Framework;
using WatchWaterConsumption;
using WaterWatch.Data;
using WatchWaterConsumption.Models;
using WatchWaterConsumption.Repositories;

namespace WatchWaterConsumption.Tests;

[TestFixture]
public class WaterConsumptionRepositoryTests
{
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WatchWaterConsumption.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static DatabaseContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new DatabaseContext(options);
    }

    [Test]
    public async Task GetAllImportsMapAndFiltersInvalidRows()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var repository = new WaterConsumptionRepository(context, new TestEnvironment());

        var rows = (await repository.GetAll()).ToList();

        Assert.That(rows, Has.Count.EqualTo(113));
        Assert.That(rows.All(row => !string.IsNullOrWhiteSpace(row.Neighbourhood)), Is.True);
        Assert.That(rows.All(row => row.AverageMonthlyKL.HasValue), Is.True);
        Assert.That(rows.All(row => !string.IsNullOrWhiteSpace(row.Coordinates)), Is.True);
    }

    [Test]
    public async Task TopTenReturnsTenValidRowsInDescendingOrder()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var repository = new WaterConsumptionRepository(context, new TestEnvironment());

        await repository.GetAll();
        var top = (await repository.GetTopTenConsumers()).ToList();

        Assert.That(top, Has.Count.EqualTo(10));
        Assert.That(top.All(row => row.AverageMonthlyKL.HasValue), Is.True);
        Assert.That(top.All(row => !string.IsNullOrWhiteSpace(row.Neighbourhood)), Is.True);

        var values = top.Select(row => row.AverageMonthlyKL!.Value).ToList();
        Assert.That(values, Is.Ordered.Descending);
        Assert.That(values[0], Is.EqualTo(43));
    }

    [Test]
    public async Task GetAllFiltersNullConsumptionRows()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        context.Consumptions.AddRange(
            new WaterConsumption { Neighbourhood = null, AverageMonthlyKL = null, Coordinates = "[]" },
            new WaterConsumption { Neighbourhood = "Ward 1", AverageMonthlyKL = 12, Coordinates = "[]" });
        await context.SaveChangesAsync();

        var repository = new WaterConsumptionRepository(context, new TestEnvironment());
        var rows = (await repository.GetAll()).ToList();

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Neighbourhood, Is.EqualTo("Ward 1"));
    }

    [Test]
    public async Task MissingGeoJsonReturnsEmptyDataWithoutThrowing()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var emptyContentRoot = Path.Combine(Path.GetTempPath(), $"waterwatch-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyContentRoot);
        try
        {
            var repository = new WaterConsumptionRepository(context, new TestEnvironment
            {
                ContentRootPath = emptyContentRoot
            });

            var rows = (await repository.GetAll()).ToList();
            Assert.That(rows, Is.Empty);
        }
        finally
        {
            Directory.Delete(emptyContentRoot, true);
        }
    }
}
