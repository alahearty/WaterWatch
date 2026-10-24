using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Security;

namespace WaterWatch.Services;

public interface IForecastService
{
    Task<DemandForecastDto> GetDemandForecastAsync(int months, CancellationToken cancellationToken = default);
    Task<WaterLevelForecastDto> GetWaterLevelForecastAsync(Guid? boreholeAssetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetRiskDto>> GetAssetFailureRiskAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeakagePriorityDto>> GetLeakagePrioritiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QualityDeteriorationDto>> GetQualityDeteriorationAsync(CancellationToken cancellationToken = default);
    Task<ForecastOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}

public class ForecastService : IForecastService
{
    private const string ModelName = "WaterWatch baseline trend";
    private const string ModelVersion = "1.0.0";
    private readonly DatabaseContext _db;
    private readonly IConsumptionService _consumption;
    private readonly IHttpContextAccessor _http;

    public ForecastService(DatabaseContext db, IConsumptionService consumption, IHttpContextAccessor http)
    {
        _db = db;
        _consumption = consumption;
        _http = http;
    }

    private bool CanViewSensitive =>
        _http.HttpContext?.User is { Identity.IsAuthenticated: true } user &&
        Policies.Definitions.First(p => p.Name == Policies.CanViewSensitive).Roles.Any(user.IsInRole);

    public async Task<DemandForecastDto> GetDemandForecastAsync(int months, CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 12);
        var trend = (await _consumption.GetTrendAsync(null, null, cancellationToken))
            .Where(t => t.ConsumptionM3 > 0).OrderBy(t => t.Period).ToList();
        if (trend.Count < 6)
        {
            return new DemandForecastDto(
                new ForecastMetadataDto("insufficient_data",
                    $"At least 6 months of consumption are required; {trend.Count} are available. No forecast was generated.",
                    ModelName, ModelVersion, null, null, trend.Count, "None",
                    new Dictionary<string, double?>(), null,
                    "Insufficient history; nothing is estimated or fabricated.", DateTimeOffset.UtcNow,
                    trend.Any() ? trend.Max(t => t.DataClassification) : DataClassification.Observed),
                Array.Empty<ForecastPointDto>(), "m3/month");
        }

        var history = trend.Select((t, i) => (X: (double)i, Y: (double)t.ConsumptionM3)).ToList();
        var fit = LinearFit(history);
        var lastPeriod = trend[^1].Period;
        var classification = trend.Any(t => t.DataClassification == DataClassification.SyntheticDemonstration)
            ? DataClassification.SyntheticDemonstration
            : trend.Any(t => t.DataClassification == DataClassification.Estimated)
                ? DataClassification.Estimated
                : DataClassification.Observed;

        // Time-series holdout validation: train on all but the last three months.
        var validationMetrics = new Dictionary<string, double?>
        {
            ["rSquared"] = fit.R2,
            ["rmseM3"] = fit.Rmse,
            ["maeM3"] = null,
            ["mapePercent"] = null
        };
        if (trend.Count >= 9)
        {
            var train = history.Take(history.Count - 3).ToList();
            var holdout = history.Skip(history.Count - 3).ToList();
            var trainFit = LinearFit(train);
            var errors = holdout.Select(h => Math.Abs(trainFit.Intercept + trainFit.Slope * h.X - h.Y)).ToList();
            validationMetrics["maeM3"] = Math.Round(errors.Average(), 2);
            validationMetrics["mapePercent"] = Math.Round(errors.Zip(holdout, (e, h) => h.Y == 0 ? 0 : e / h.Y).Average() * 100, 2);
        }

        var points = new List<ForecastPointDto>();
        for (var i = trend.Count - Math.Min(6, trend.Count); i < trend.Count; i++)
        {
            points.Add(new ForecastPointDto(trend[i].Period, (double)trend[i].ConsumptionM3, null, null, "m3/month", false));
        }
        var lastDate = DateOnly.ParseExact(lastPeriod + "-01", "yyyy-MM-dd");
        var band = fit.Rmse * 1.96;
        for (var i = 1; i <= months; i++)
        {
            var date = lastDate.AddMonths(i);
            var value = Math.Max(0, fit.Intercept + fit.Slope * (trend.Count - 1 + i));
            points.Add(new ForecastPointDto($"{date:yyyy-MM}", Math.Round(value, 2),
                Math.Round(Math.Max(0, value - band), 2), Math.Round(value + band, 2), "m3/month", true));
        }

        return new DemandForecastDto(
            new ForecastMetadataDto("available", null, ModelName, ModelVersion,
                DateOnly.ParseExact(trend[0].Period + "-01", "yyyy-MM-dd"),
                DateOnly.ParseExact(lastPeriod + "-01", "yyyy-MM-dd"), trend.Count,
                "Ordinary least squares with a 3-month holdout where history permits; 95% band = ±1.96 × residual RMSE.",
                validationMetrics,
                "Prediction bands assume independent residuals and do not fully capture seasonal or demand-shock effects.",
                "Baseline statistical forecast, not an operational demand plan. Synthetic/estimated history is labelled in the input series.",
                DateTimeOffset.UtcNow, classification),
            points, "m3/month");
    }

    public async Task<WaterLevelForecastDto> GetWaterLevelForecastAsync(Guid? boreholeAssetId, CancellationToken cancellationToken = default)
    {
        var allReadings = await _db.GroundwaterReadings.AsNoTracking()
            .Where(r => r.WaterLevelM != null).OrderBy(r => r.ObservedAt).ToListAsync(cancellationToken);
        if (boreholeAssetId is null && allReadings.Count > 0)
        {
            boreholeAssetId = allReadings.GroupBy(r => r.BoreholeAssetId)
                .OrderByDescending(g => g.Count()).First().Key;
        }
        var readings = boreholeAssetId is null
            ? allReadings
            : allReadings.Where(r => r.BoreholeAssetId == boreholeAssetId).ToList();
        if (readings.Count < 8)
        {
            var name = boreholeAssetId is null ? "No borehole selected" :
                await _db.Assets.Where(a => a.Id == boreholeAssetId).Select(a => a.Name).FirstOrDefaultAsync(cancellationToken) ?? "Unknown borehole";
            return new WaterLevelForecastDto(
                new ForecastMetadataDto("insufficient_data",
                    $"At least 8 water-level readings are required; {readings.Count} are available.",
                    ModelName, ModelVersion, null, null, readings.Count, "None",
                    new Dictionary<string, double?>(), null,
                    "Insufficient observations; no water-level forecast was generated.", DateTimeOffset.UtcNow,
                    DataClassification.Observed),
                boreholeAssetId ?? Guid.Empty, name, Array.Empty<ForecastPointDto>(), "m");
        }

        var start = readings[0].ObservedAt;
        var history = readings.Select(r => (X: (r.ObservedAt - start).TotalDays / 30.4375, Y: (double)r.WaterLevelM!.Value)).ToList();
        var fit = LinearFit(history);
        var last = readings[^1];
        var points = readings.TakeLast(8).Select(r => new ForecastPointDto(
            r.ObservedAt.ToString("yyyy-MM-dd"), (double)r.WaterLevelM!.Value, null, null, "m", false)).ToList();
        var band = fit.Rmse * 1.96;
        for (var i = 1; i <= 3; i++)
        {
            var date = last.ObservedAt.AddMonths(i);
            var x = (date - start).TotalDays / 30.4375;
            var value = fit.Intercept + fit.Slope * x;
            points.Add(new ForecastPointDto(date.ToString("yyyy-MM"),
                Math.Round(Math.Max(0, value), 3),
                Math.Round(Math.Max(0, value - band), 3), Math.Round(value + band, 3), "m", true));
        }
        var assetName = await _db.Assets.Where(a => a.Id == last.BoreholeAssetId).Select(a => a.Name).FirstOrDefaultAsync(cancellationToken) ?? "Borehole";
        return new WaterLevelForecastDto(
            new ForecastMetadataDto("available", null, ModelName, ModelVersion,
                DateOnly.FromDateTime(readings[0].ObservedAt.Date), DateOnly.FromDateTime(last.ObservedAt.Date),
                readings.Count, "Ordinary least squares on monthly-equivalent time; 95% band = ±1.96 × residual RMSE.",
                new Dictionary<string, double?> { ["rSquared"] = fit.R2, ["rmseM"] = fit.Rmse },
                "Short series and pumping interference reduce forecast reliability.",
                "Not a hydrogeological model; use for trend screening only.", DateTimeOffset.UtcNow,
                readings.Any(r => r.DataClassification == DataClassification.SyntheticDemonstration)
                    ? DataClassification.SyntheticDemonstration : DataClassification.Observed),
            last.BoreholeAssetId, assetName, points, "m");
    }

    public async Task<IReadOnlyList<AssetRiskDto>> GetAssetFailureRiskAsync(CancellationToken cancellationToken = default)
    {
        var assets = await _db.Assets.AsNoTracking().ToListAsync(cancellationToken);
        var inspections = await _db.Inspections.AsNoTracking().GroupBy(i => i.AssetId)
            .Select(g => new { AssetId = g.Key, Last = g.Max(i => i.CompletedAt ?? i.ScheduledFor) })
            .ToListAsync(cancellationToken);
        var inspectionMap = inspections.ToDictionary(i => i.AssetId, i => i.Last);
        var openOrders = await _db.WorkOrders.AsNoTracking()
            .Where(w => w.AssetId != null && w.Status != WorkOrderStatus.Closed && w.Status != WorkOrderStatus.Cancelled)
            .GroupBy(w => w.AssetId!.Value).Select(g => new { AssetId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var openMap = openOrders.ToDictionary(o => o.AssetId, o => o.Count);
        var leaks = await _db.LeakIncidents.AsNoTracking().Where(l => l.Geometry != null && l.AssetId != null).ToListAsync(cancellationToken);
        var results = new List<AssetRiskDto>();
        var now = DateTimeOffset.UtcNow;
        foreach (var asset in assets)
        {
            if (asset.IsSensitive && !CanViewSensitive)
            {
                continue;
            }
            var factors = new List<string>();
            var score = 0;
            score += asset.Condition switch
            {
                ConditionGrade.Critical => 40,
                ConditionGrade.Poor => 30,
                ConditionGrade.Fair => 15,
                ConditionGrade.Good => 5,
                ConditionGrade.Excellent => 0,
                _ => 10
            };
            if (asset.Condition is not (ConditionGrade.Excellent or ConditionGrade.Good or ConditionGrade.Fair))
            {
                factors.Add($"Condition: {asset.Condition}");
            }
            double? age = null;
            if (asset.InstalledOn is not null)
            {
                age = (DateOnly.FromDateTime(DateTime.UtcNow) .DayNumber - asset.InstalledOn.Value.DayNumber) / 365.25;
                if (age >= 30) { score += 25; factors.Add($"Age {age:0.0} years"); }
                else if (age >= 20) { score += 18; factors.Add($"Age {age:0.0} years"); }
                else if (age >= 10) { score += 10; factors.Add($"Age {age:0.0} years"); }
            }
            else
            {
                score += 5;
                factors.Add("Installation date unknown");
            }
            var open = openMap.GetValueOrDefault(asset.Id);
            if (open > 0)
            {
                score += Math.Min(20, open * 10);
                factors.Add($"{open} open work order(s)");
            }
            var nearbyLeaks = asset.Geometry is null ? 0 : leaks.Count(l =>
                l.Geometry is not null &&
                SpatialMath.ToUtm32(l.Geometry).Distance(SpatialMath.ToUtm32(asset.Geometry)) <= 250);
            if (nearbyLeaks > 0)
            {
                score += Math.Min(15, nearbyLeaks * 5);
                factors.Add($"{nearbyLeaks} leak incident(s) within 250 m");
            }
            if (!inspectionMap.TryGetValue(asset.Id, out var lastInspection) || lastInspection < now.AddYears(-2))
            {
                score += 10;
                factors.Add("No inspection in the last 2 years");
            }
            score = Math.Clamp(score, 0, 100);
            var band = score >= 70 ? "High" : score >= 45 ? "Medium" : "Low";
            results.Add(new AssetRiskDto(asset.Id, asset.AssetCode, asset.Name, asset.AssetType, asset.Status,
                asset.Condition, score, band, factors, age is null ? null : Math.Round(age.Value, 1),
                open > 0, nearbyLeaks, DataClassification.Modelled));
        }
        return results.OrderByDescending(r => r.RiskScore).Take(20).ToList();
    }

    public async Task<IReadOnlyList<LeakagePriorityDto>> GetLeakagePrioritiesAsync(CancellationToken cancellationToken = default)
    {
        var dmas = await _db.OperationalAreas.AsNoTracking()
            .Where(a => a.AreaType == OperationalAreaType.DistrictMeteredArea).ToListAsync(cancellationToken);
        var balances = await _db.DmaWaterBalances.AsNoTracking().ToListAsync(cancellationToken);
        var leaks = await _db.LeakIncidents.AsNoTracking().Where(l => l.DmaId != null).ToListAsync(cancellationToken);
        var results = new List<LeakagePriorityDto>();
        foreach (var dma in dmas)
        {
            var dmaBalances = balances.Where(b => b.DmaId == dma.Id).ToList();
            var latest = dmaBalances.OrderByDescending(b => b.PeriodEnd).FirstOrDefault();
            if (latest is null)
            {
                continue;
            }
            var nrw = latest.InputVolumeM3 - latest.BilledVolumeM3;
            var nrwPercent = latest.InputVolumeM3 == 0 ? 0 : (double)(nrw / latest.InputVolumeM3 * 100);
            var dmaLeaks = leaks.Where(l => l.DmaId == dma.Id && l.Status is not (LeakStatus.Closed or LeakStatus.Verified)).ToList();
            var factors = new List<string> { $"NRW {nrwPercent:0.0}% in {latest.PeriodEnd:yyyy-MM}" };
            var score = (int)Math.Round(Math.Min(50, nrwPercent));
            if (dmaLeaks.Count > 0)
            {
                score += Math.Min(30, dmaLeaks.Count * 8);
                factors.Add($"{dmaLeaks.Count} open leak incident(s)");
            }
            var estimatedLoss = dmaLeaks.Sum(l => l.EstimatedLossM3PerHour ?? 0);
            if (estimatedLoss > 0)
            {
                score += Math.Min(20, (int)Math.Round((double)estimatedLoss));
                factors.Add($"Estimated leak loss {estimatedLoss:0.##} m³/h (estimate)");
            }
            var classification = latest.DataClassification == DataClassification.SyntheticDemonstration ||
                                 dmaLeaks.Any(l => l.DataClassification == DataClassification.SyntheticDemonstration)
                ? DataClassification.SyntheticDemonstration : DataClassification.Modelled;
            results.Add(new LeakagePriorityDto(dma.Id, dma.Name, Math.Round((decimal)nrwPercent, 1), dmaLeaks.Count,
                estimatedLoss, Math.Clamp(score, 0, 100), score >= 65 ? "Investigate now" : score >= 40 ? "Monitor" : "Low",
                factors, classification));
        }
        return results.OrderByDescending(r => r.RiskScore).ToList();
    }

    public async Task<IReadOnlyList<QualityDeteriorationDto>> GetQualityDeteriorationAsync(CancellationToken cancellationToken = default)
    {
        var contaminants = new HashSet<ParameterCode>
        {
            ParameterCode.TurbidityNTU, ParameterCode.NitrateMgL, ParameterCode.LeadMgL,
            ParameterCode.IronMgL, ParameterCode.TotalColiformCfu100ml, ParameterCode.EColiCfu100ml,
            ParameterCode.HydrocarbonMgL, ParameterCode.TotalDissolvedSolidsMgL,
            ParameterCode.ElectricalConductivityUScm, ParameterCode.ChlorideMgL
        };
        var results = await _db.SampleResults.AsNoTracking()
            .Include(r => r.Sample).ThenInclude(s => s.Station)
            .Where(r => r.Value != null && contaminants.Contains(r.Parameter))
            .ToListAsync(cancellationToken);
        var rows = new List<QualityDeteriorationDto>();
        foreach (var group in results.GroupBy(r => new { r.Sample.StationId, r.Parameter }))
        {
            var ordered = group.OrderBy(r => r.Sample.SampledAt).ToList();
            if (ordered.Count < 4)
            {
                continue;
            }
            var start = ordered[0].Sample.SampledAt;
            var history = ordered.Select(r => (X: (r.Sample.SampledAt - start).TotalDays / 30.4375, Y: (double)r.Value!.Value)).ToList();
            var fit = LinearFit(history);
            var direction = fit.Slope > 0.0001 ? "Increasing" : fit.Slope < -0.0001 ? "Decreasing" : "Stable";
            rows.Add(new QualityDeteriorationDto(group.Key.StationId, ordered[0].Sample.Station.Name,
                group.Key.Parameter, ordered.Count, Math.Round(fit.Slope, 4), direction,
                direction == "Increasing"
                    ? "Increasing contaminant trend; investigate source and confirm with additional sampling."
                    : direction == "Decreasing" ? "Decreasing trend; continue monitoring." : "No material monotonic trend detected.",
                ordered.Any(r => r.DataClassification == DataClassification.SyntheticDemonstration)
                    ? DataClassification.SyntheticDemonstration : DataClassification.Observed));
        }
        return rows.OrderByDescending(r => r.SlopePerMonth).Take(30).ToList();
    }

    public async Task<ForecastOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var demand = await GetDemandForecastAsync(6, cancellationToken);
        var risks = await GetAssetFailureRiskAsync(cancellationToken);
        var leakage = await GetLeakagePrioritiesAsync(cancellationToken);
        var quality = await GetQualityDeteriorationAsync(cancellationToken);
        var message = demand.Metadata.Status == "available"
            ? "Baseline forecasts and rule-based risk rankings are available with their metadata and limitations."
            : "Forecasts are unavailable until sufficient consumption history exists. Risk rankings use registered asset/leak data only.";
        return new ForecastOverviewDto(demand, risks, leakage, quality, message);
    }

    private static LinearFitResult LinearFit(IReadOnlyList<(double X, double Y)> points)
    {
        var n = points.Count;
        var meanX = points.Average(p => p.X);
        var meanY = points.Average(p => p.Y);
        var sxx = points.Sum(p => (p.X - meanX) * (p.X - meanX));
        var sxy = points.Sum(p => (p.X - meanX) * (p.Y - meanY));
        var slope = sxx == 0 ? 0 : sxy / sxx;
        var intercept = meanY - slope * meanX;
        var residuals = points.Select(p => p.Y - (intercept + slope * p.X)).ToList();
        var sse = residuals.Sum(r => r * r);
        var sst = points.Sum(p => (p.Y - meanY) * (p.Y - meanY));
        var r2 = sst == 0 ? 0 : 1 - sse / sst;
        var rmse = n == 0 ? 0 : Math.Sqrt(sse / n);
        return new LinearFitResult(slope, intercept, r2, rmse);
    }

    private sealed record LinearFitResult(double Slope, double Intercept, double R2, double Rmse);
}
