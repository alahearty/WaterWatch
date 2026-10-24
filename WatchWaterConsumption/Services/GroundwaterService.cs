using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface IGroundwaterService
{
    Task<IReadOnlyList<BoreholeDto>> GetBoreholesAsync(CancellationToken cancellationToken = default);
    Task<BoreholeDetailDto?> GetBoreholeAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<BoreholeDetailDto> UpsertDetailAsync(Guid assetId, BoreholeDetailUpsertRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroundwaterReadingDto>> GetReadingsAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<GroundwaterReadingDto> AddReadingAsync(Guid assetId, GroundwaterReadingCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PumpingTestDto>> GetPumpingTestsAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<PumpingTestDto> AddPumpingTestAsync(Guid assetId, PumpingTestCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroundwaterTrendDto>> GetTrendAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaminationProximityDto>> GetNearbyContaminationAsync(Guid assetId, double radiusMeters, CancellationToken cancellationToken = default);
    Task<GroundwaterSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public class GroundwaterService : IGroundwaterService
{
    private readonly DatabaseContext _db;

    public GroundwaterService(DatabaseContext db) => _db = db;

    public async Task<IReadOnlyList<BoreholeDto>> GetBoreholesAsync(CancellationToken cancellationToken = default)
    {
        var assets = await _db.Assets.AsNoTracking()
            .Where(a => a.AssetType == AssetType.Borehole || a.AssetType == AssetType.Well)
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var details = await _db.BoreholeDetails.AsNoTracking().ToListAsync(cancellationToken);
        var detailMap = details.ToDictionary(d => d.AssetId);
        var readings = await _db.GroundwaterReadings.AsNoTracking()
            .OrderByDescending(r => r.ObservedAt).ToListAsync(cancellationToken);
        var latest = readings.GroupBy(r => r.BoreholeAssetId).ToDictionary(g => g.Key, g => g.First());
        return assets.Select(a =>
        {
            detailMap.TryGetValue(a.Id, out var detail);
            latest.TryGetValue(a.Id, out var reading);
            return new BoreholeDto(a.Id, a.AssetCode, a.Name, a.Status, a.Condition,
                detail?.BoreholeDepthM, detail?.AquiferName, detail?.AquiferClassificationInferred ?? true,
                detail?.StaticWaterLevelM, detail?.DynamicWaterLevelM, detail?.YieldM3PerHour,
                reading?.ObservedAt, reading?.WaterLevelM,
                a.Geometry?.Centroid.X, a.Geometry?.Centroid.Y, a.DataClassification);
        }).ToList();
    }

    public async Task<BoreholeDetailDto?> GetBoreholeAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assetId && (a.AssetType == AssetType.Borehole || a.AssetType == AssetType.Well), cancellationToken);
        if (asset is null)
        {
            return null;
        }
        var detail = await _db.BoreholeDetails.AsNoTracking().FirstOrDefaultAsync(d => d.AssetId == assetId, cancellationToken);
        return ToDto(asset, detail);
    }

    public async Task<BoreholeDetailDto> UpsertDetailAsync(Guid assetId, BoreholeDetailUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
            ?? throw new KeyNotFoundException("Asset not found.");
        if (asset.AssetType is not (AssetType.Borehole or AssetType.Well))
        {
            throw new InvalidOperationException("Groundwater details can only be attached to a borehole or well asset.");
        }
        if (request.ScreenTopM is not null && request.ScreenBottomM is not null && request.ScreenTopM >= request.ScreenBottomM)
        {
            throw new ArgumentException("Screen top must be shallower than screen bottom.");
        }
        if (request.BoreholeDepthM is not null && request.ScreenBottomM is not null && request.ScreenBottomM > request.BoreholeDepthM)
        {
            throw new ArgumentException("Screen bottom cannot exceed borehole depth.");
        }
        var detail = await _db.BoreholeDetails.FirstOrDefaultAsync(d => d.AssetId == assetId, cancellationToken);
        if (detail is null)
        {
            detail = new BoreholeDetail { AssetId = assetId };
            _db.BoreholeDetails.Add(detail);
        }
        detail.BoreholeDepthM = request.BoreholeDepthM;
        detail.ScreenTopM = request.ScreenTopM;
        detail.ScreenBottomM = request.ScreenBottomM;
        detail.AquiferName = request.AquiferName;
        detail.AquiferClassification = request.AquiferClassification;
        detail.AquiferClassificationInferred = request.AquiferClassificationInferred;
        detail.StaticWaterLevelM = request.StaticWaterLevelM;
        detail.DynamicWaterLevelM = request.DynamicWaterLevelM;
        detail.YieldM3PerHour = request.YieldM3PerHour;
        detail.DrilledOn = request.DrilledOn;
        detail.Driller = request.Driller;
        detail.ConstructionJson = NormaliseJson(request.ConstructionJson);
        detail.LithologyJson = NormaliseJson(request.LithologyJson);
        detail.DataClassification = DataClassification.Observed;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(asset, detail);
    }

    public async Task<IReadOnlyList<GroundwaterReadingDto>> GetReadingsAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        await _db.GroundwaterReadings.AsNoTracking().Include(r => r.DataSource)
            .Where(r => r.BoreholeAssetId == assetId)
            .OrderByDescending(r => r.ObservedAt)
            .Select(r => new GroundwaterReadingDto(r.Id, r.BoreholeAssetId, r.ObservedAt, r.WaterLevelM,
                r.AbstractionM3, r.Notes, r.DataSource == null ? null : r.DataSource.Name, r.DataClassification))
            .ToListAsync(cancellationToken);

    public async Task<GroundwaterReadingDto> AddReadingAsync(Guid assetId, GroundwaterReadingCreateRequest request, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
            ?? throw new KeyNotFoundException("Asset not found.");
        if (asset.AssetType is not (AssetType.Borehole or AssetType.Well))
        {
            throw new InvalidOperationException("Groundwater readings require a borehole or well asset.");
        }
        if (request.WaterLevelM is null && request.AbstractionM3 is null)
        {
            throw new ArgumentException("A water level or abstraction volume is required.");
        }
        var reading = new GroundwaterReading
        {
            BoreholeAssetId = assetId, ObservedAt = request.ObservedAt, WaterLevelM = request.WaterLevelM,
            AbstractionM3 = request.AbstractionM3, Notes = request.Notes,
            DataClassification = request.DataClassification
        };
        _db.GroundwaterReadings.Add(reading);
        await _db.SaveChangesAsync(cancellationToken);
        return new GroundwaterReadingDto(reading.Id, reading.BoreholeAssetId, reading.ObservedAt,
            reading.WaterLevelM, reading.AbstractionM3, reading.Notes, null, reading.DataClassification);
    }

    public async Task<IReadOnlyList<PumpingTestDto>> GetPumpingTestsAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        await _db.PumpingTests.AsNoTracking().Where(t => t.BoreholeAssetId == assetId)
            .OrderByDescending(t => t.TestedAt)
            .Select(t => new PumpingTestDto(t.Id, t.BoreholeAssetId, t.TestedAt, t.DurationMinutes,
                t.PumpingRateM3PerHour, t.DrawdownM, t.RecoveryMinutes, t.ConductedBy, t.ResultJson, t.DataClassification))
            .ToListAsync(cancellationToken);

    public async Task<PumpingTestDto> AddPumpingTestAsync(Guid assetId, PumpingTestCreateRequest request, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
            ?? throw new KeyNotFoundException("Asset not found.");
        if (asset.AssetType is not (AssetType.Borehole or AssetType.Well))
        {
            throw new InvalidOperationException("Pumping tests require a borehole or well asset.");
        }
        var test = new PumpingTest
        {
            BoreholeAssetId = assetId, TestedAt = request.TestedAt, DurationMinutes = request.DurationMinutes,
            PumpingRateM3PerHour = request.PumpingRateM3PerHour, DrawdownM = request.DrawdownM,
            RecoveryMinutes = request.RecoveryMinutes, ConductedBy = request.ConductedBy,
            ResultJson = NormaliseJson(request.ResultJson), DataClassification = request.DataClassification
        };
        _db.PumpingTests.Add(test);
        await _db.SaveChangesAsync(cancellationToken);
        return new PumpingTestDto(test.Id, test.BoreholeAssetId, test.TestedAt, test.DurationMinutes,
            test.PumpingRateM3PerHour, test.DrawdownM, test.RecoveryMinutes, test.ConductedBy,
            test.ResultJson, test.DataClassification);
    }

    public async Task<IReadOnlyList<GroundwaterTrendDto>> GetTrendAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var readings = await _db.GroundwaterReadings.AsNoTracking()
            .Where(r => r.BoreholeAssetId == assetId).OrderBy(r => r.ObservedAt)
            .ToListAsync(cancellationToken);
        return readings.GroupBy(r => new { r.ObservedAt.Year, r.ObservedAt.Month })
            .Select(g =>
            {
                var levels = g.Where(r => r.WaterLevelM != null).Select(r => r.WaterLevelM!.Value).ToList();
                return new GroundwaterTrendDto($"{g.Key.Year:D4}-{g.Key.Month:D2}",
                    levels.Count == 0 ? null : Math.Round(levels.Average(), 3),
                    g.Sum(r => r.AbstractionM3) ?? 0,
                    g.Count());
            })
            .OrderBy(t => t.Period).ToList();
    }

    public async Task<IReadOnlyList<ContaminationProximityDto>> GetNearbyContaminationAsync(Guid assetId, double radiusMeters, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
            ?? throw new KeyNotFoundException("Asset not found.");
        if (asset.Geometry is null)
        {
            return Array.Empty<ContaminationProximityDto>();
        }
        radiusMeters = Math.Clamp(radiusMeters, 50, 10000);
        var stations = await _db.SamplingStations.AsNoTracking()
            .Include(s => s.Samples).ThenInclude(s => s.Results)
            .Where(s => s.Geometry != null).ToListAsync(cancellationToken);
        var assetUtm = SpatialMath.ToUtm32(asset.Geometry);
        var results = new List<ContaminationProximityDto>();
        foreach (var station in stations)
        {
            if (station.Geometry is null)
            {
                continue;
            }
            var distance = assetUtm.Distance(SpatialMath.ToUtm32(station.Geometry));
            if (distance > radiusMeters)
            {
                continue;
            }
            var latest = station.Samples.OrderByDescending(s => s.SampledAt).FirstOrDefault();
            var nonCompliant = latest?.Results.Where(r => r.State == QualityState.NonCompliant).ToList() ?? new List<SampleResult>();
            results.Add(new ContaminationProximityDto(station.Id, station.Name, Math.Round(distance, 1),
                latest?.Results.OrderByDescending(r => r.State == QualityState.NonCompliant).FirstOrDefault()?.State.ToString(),
                nonCompliant.Count, nonCompliant.Select(r => r.Parameter.ToString()).ToList(),
                station.DataClassification));
        }
        return results.OrderBy(r => r.DistanceM).ToList();
    }

    public async Task<GroundwaterSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var boreholes = await _db.Assets.CountAsync(a => a.AssetType == AssetType.Borehole || a.AssetType == AssetType.Well, cancellationToken);
        var details = await _db.BoreholeDetails.CountAsync(cancellationToken);
        var readings = await _db.GroundwaterReadings.CountAsync(cancellationToken);
        var tests = await _db.PumpingTests.CountAsync(cancellationToken);
        var inferred = await _db.BoreholeDetails.CountAsync(d => d.AquiferClassificationInferred, cancellationToken);
        var last = await _db.GroundwaterReadings.OrderByDescending(r => r.ObservedAt)
            .Select(r => (DateTimeOffset?)r.ObservedAt).FirstOrDefaultAsync(cancellationToken);
        var message = boreholes == 0
            ? "No borehole or well assets are registered. Register an asset in Infrastructure before recording groundwater data."
            : readings == 0
                ? "Boreholes are registered but no groundwater readings have been recorded."
                : "Observed water levels are separated from inferred aquifer classifications.";
        return new GroundwaterSummaryDto(boreholes, details, readings, tests, inferred, last, message);
    }

    private static BoreholeDetailDto ToDto(Asset asset, BoreholeDetail? detail) => new(
        asset.Id, asset.AssetCode, asset.Name, asset.Status, asset.Condition,
        detail?.BoreholeDepthM, detail?.ScreenTopM, detail?.ScreenBottomM, detail?.AquiferName,
        detail?.AquiferClassification, detail?.AquiferClassificationInferred ?? true,
        detail?.StaticWaterLevelM, detail?.DynamicWaterLevelM, detail?.YieldM3PerHour,
        detail?.DrilledOn, detail?.Driller, detail?.ConstructionJson, detail?.LithologyJson,
        detail?.DataClassification ?? asset.DataClassification);

    private static string? NormaliseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            using var _ = JsonDocument.Parse(json);
            return json;
        }
        catch (JsonException)
        {
            throw new ArgumentException("The supplied JSON is not valid.");
        }
    }
}
