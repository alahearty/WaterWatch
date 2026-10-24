using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using WatchWaterConsumption.Models;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Services;

namespace WatchWaterConsumption.Tests;

[TestFixture]
public class DomainCalculationTests
{
    private static DatabaseContext Context(string name)
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>().UseInMemoryDatabase(name).Options;
        return new DatabaseContext(options);
    }

    [Test]
    public void WaterUnitConversionsAreExplicit()
    {
        WaterUnits.CubicMetresToLitres(1.5m).Should().Be(1500m);
        WaterUnits.CubicMetresToKilolitres(1.5m).Should().Be(1.5m);
        WaterUnits.LitresToCubicMetres(2500m).Should().Be(2.5m);
        WaterUnits.LitresPerCapitaPerDay(10m, 1000, 10).Should().Be(1m);
        WaterUnits.LitresPerCapitaPerDay(10m, null, 10).Should().BeNull();
    }

    [Test]
    public void QualityAssessmentHonoursEveryEvidenceState()
    {
        var limit = new QualityLimit { Parameter = ParameterCode.pH, Unit = "pH units", MinValue = 6.5m, MaxValue = 8.5m };
        QualityService.Assess(7.2m, "pH units", limit).State.Should().Be(QualityState.Compliant);
        QualityService.Assess(5.5m, "pH units", limit).State.Should().Be(QualityState.NonCompliant);
        QualityService.Assess(9m, "pH units", limit).State.Should().Be(QualityState.NonCompliant);
        QualityService.Assess(null, "pH units", limit).State.Should().Be(QualityState.InsufficientEvidence);
        QualityService.Assess(7.2m, "mg/L", limit).State.Should().Be(QualityState.NotAssessed);
        QualityService.Assess(7.2m, "pH units", null).State.Should().Be(QualityState.NotAssessed);
    }

    [Test]
    public void SpatialMathRoundTripsAndBuffersInMetres()
    {
        var point = SpatialMath.Point(7.0498, 4.8156);
        var utm = SpatialMath.ToUtm32(point);
        var back = SpatialMath.ToWgs84(utm);
        back.Coordinate.X.Should().BeApproximately(7.0498, 1e-6);
        back.Coordinate.Y.Should().BeApproximately(4.8156, 1e-6);

        var buffer = SpatialMath.BufferGeometryMeters(point, 100);
        var area = SpatialMath.AreaSquareKilometers(buffer);
        area.Should().BeInRange(0.028, 0.033);
    }

    [Test]
    public async Task ConsumptionHandlesEstimatedReadingsAndRollover()
    {
        await using var db = Context(Guid.NewGuid().ToString());
        var zone = new OperationalArea { Code = "Z1", Name = "Zone 1", AreaType = OperationalAreaType.ServiceZone };
        var account = new ConsumerAccount
        {
            AccountNumber = "A1", Name = "Test", Category = ConsumerCategory.Domestic, ServiceZone = zone
        };
        var meter = new Meter { SerialNumber = "M1", ConsumerAccount = account, RolloverValueM3 = 200m };
        meter.Readings.Add(new MeterReading { ReadingDate = new DateOnly(2026, 1, 1), ReadingValueM3 = 100m, ReadingType = ReadingType.Observed });
        meter.Readings.Add(new MeterReading { ReadingDate = new DateOnly(2026, 2, 1), ReadingValueM3 = 130m, ReadingType = ReadingType.Observed });
        meter.Readings.Add(new MeterReading { ReadingDate = new DateOnly(2026, 3, 1), ReadingValueM3 = 160m, ReadingType = ReadingType.Estimated });
        meter.Readings.Add(new MeterReading { ReadingDate = new DateOnly(2026, 4, 1), ReadingValueM3 = 20m, ReadingType = ReadingType.Rollover });
        db.OperationalAreas.Add(zone);
        db.ConsumerAccounts.Add(account);
        db.Meters.Add(meter);
        await db.SaveChangesAsync();

        var service = new ConsumptionService(db, new GeoJsonService());
        var summary = await service.GetSummaryAsync(null, null);
        summary.TotalM3.Should().Be(120m);
        summary.TotalLitres.Should().Be(120000m);
        summary.EstimatedSharePercent.Should().Be(75m); // the rollover interval also starts from an estimated cumulative value
        summary.Accounts.Should().Be(1);
        summary.Meters.Should().Be(1);

        var trend = await service.GetTrendAsync(null, null);
        trend.Should().HaveCount(3);
        trend.Select(t => t.Period).Should().ContainInOrder("2026-02", "2026-03", "2026-04");

        var aggregates = await service.GetAggregatesAsync("servicezone", null, null);
        aggregates.Should().ContainSingle();
        aggregates[0].Label.Should().Be("Zone 1");
        aggregates[0].ConsumptionM3.Should().Be(120m);
    }

    [Test]
    public async Task NrwBalanceKeepsUnevidencedLossUnallocated()
    {
        await using var db = Context(Guid.NewGuid().ToString());
        var dma = new OperationalArea { Code = "D1", Name = "DMA 1", AreaType = OperationalAreaType.DistrictMeteredArea };
        db.OperationalAreas.Add(dma);
        db.DmaWaterBalances.Add(new DmaWaterBalance
        {
            Dma = dma, PeriodStart = new DateOnly(2026, 1, 1), PeriodEnd = new DateOnly(2026, 1, 31),
            InputVolumeM3 = 1000m, BilledVolumeM3 = 700m,
            ApparentLossesM3 = 50m, RealLossesM3 = 150m
        });
        db.DmaWaterBalances.Add(new DmaWaterBalance
        {
            Dma = dma, PeriodStart = new DateOnly(2026, 2, 1), PeriodEnd = new DateOnly(2026, 2, 28),
            InputVolumeM3 = 1000m, BilledVolumeM3 = 700m
        });
        await db.SaveChangesAsync();

        var service = new NrwService(db);
        var summary = await service.GetSummaryAsync(null, null);
        summary.NrwM3.Should().Be(600m);
        summary.NrwPercent.Should().Be(30m);
        summary.UnallocatedM3.Should().Be(400m);
        summary.AvailabilityMessage.Should().Contain("unallocated");

        var balances = await service.GetBalancesAsync(dma.Id, null, null);
        balances.Should().HaveCount(2);
        balances.First(b => b.PeriodStart.Month == 1).LossSplitEvidence.Should().Be("Evidenced");
        balances.First(b => b.PeriodStart.Month == 2).LossSplitEvidence.Should().Be("Unallocated");
    }

    [Test]
    public async Task WorkOrderWorkflowEnforcesTransitionsAndSegregationOfDuties()
    {
        await using var db = Context(Guid.NewGuid().ToString());
        var service = new WorkOrderService(db);
        var created = await service.CreateAsync(new WaterWatch.Dtos.WorkOrderCreateRequest
        {
            Title = "Test work order", Type = WorkOrderType.Defect, Priority = Priority.Medium
        }, "reporter");
        created.WorkOrder.Status.Should().Be(WorkOrderStatus.Reported);

        await service.TransitionAsync(created.WorkOrder.Id, new WaterWatch.Dtos.WorkOrderTransitionRequest
        {
            Status = WorkOrderStatus.Triaged, Notes = "triaged"
        }, "engineer");

        var invalid = async () => await service.TransitionAsync(created.WorkOrder.Id,
            new WaterWatch.Dtos.WorkOrderTransitionRequest { Status = WorkOrderStatus.Closed }, "engineer");
        await invalid.Should().ThrowAsync<InvalidOperationException>();

        await service.TransitionAsync(created.WorkOrder.Id, new WaterWatch.Dtos.WorkOrderTransitionRequest
        {
            Status = WorkOrderStatus.Assigned, AssignedTo = "inspector", Notes = "assigned"
        }, "engineer");
        await service.TransitionAsync(created.WorkOrder.Id, new WaterWatch.Dtos.WorkOrderTransitionRequest
        {
            Status = WorkOrderStatus.InProgress, Notes = "started"
        }, "inspector");
        await service.TransitionAsync(created.WorkOrder.Id, new WaterWatch.Dtos.WorkOrderTransitionRequest
        {
            Status = WorkOrderStatus.Resolved, Notes = "resolved"
        }, "engineer");

        var sameUser = async () => await service.TransitionAsync(created.WorkOrder.Id,
            new WaterWatch.Dtos.WorkOrderTransitionRequest { Status = WorkOrderStatus.Verified, Notes = "verify" }, "engineer");
        await sameUser.Should().ThrowAsync<InvalidOperationException>();

        var verified = await service.TransitionAsync(created.WorkOrder.Id, new WaterWatch.Dtos.WorkOrderTransitionRequest
        {
            Status = WorkOrderStatus.Verified, Notes = "verified by reviewer"
        }, "reviewer");
        verified.WorkOrder.Status.Should().Be(WorkOrderStatus.Verified);
        var closed = await service.TransitionAsync(created.WorkOrder.Id, new WaterWatch.Dtos.WorkOrderTransitionRequest
        {
            Status = WorkOrderStatus.Closed, Notes = "closed"
        }, "reviewer");
        closed.WorkOrder.Status.Should().Be(WorkOrderStatus.Closed);
        closed.Events.Should().HaveCountGreaterThanOrEqualTo(7);
    }
}

[TestFixture]
public class GeoJsonValidationTests
{
    [Test]
    public void NigeriaEnvelopeAndOgcValidityAreChecked()
    {
        var service = new GeoJsonService();
        service.Validate(SpatialMath.Point(7.0498, 4.8156)).Should().BeEmpty();
        service.Validate(SpatialMath.Point(0.1, 51.5)).Should().ContainMatch("*outside*");
    }
}
