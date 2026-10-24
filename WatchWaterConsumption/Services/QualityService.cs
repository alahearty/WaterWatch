using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface IQualityService
{
    Task<IReadOnlyList<QualityStandardDto>> GetStandardsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SamplingStationDto>> GetStationsAsync(CancellationToken cancellationToken = default);
    Task<SamplingStationDto> CreateStationAsync(SamplingStationCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SamplingCampaignDto>> GetCampaignsAsync(CancellationToken cancellationToken = default);
    Task<SamplingCampaignDto> CreateCampaignAsync(CampaignCreateRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<WaterSampleDto>> GetSamplesAsync(QualityQuery query, CancellationToken cancellationToken = default);
    Task<WaterSampleDto?> GetSampleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WaterSampleDto> CreateSampleAsync(SampleCreateRequest request, string userName, CancellationToken cancellationToken = default);
    Task<SampleResultDto> AddResultAsync(Guid sampleId, SampleResultEntry entry, CancellationToken cancellationToken = default);
    Task<QualitySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QualityExceedanceDto>> GetExceedancesAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QualityHotspotDto>> GetHotspotsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResultTrendDto>> GetTrendAsync(Guid stationId, ParameterCode parameter, CancellationToken cancellationToken = default);
    Task<string> GetChoroplethAsync(CancellationToken cancellationToken = default);
}

public class QualityService : IQualityService
{
    private readonly DatabaseContext _db;
    private readonly IGeoJsonService _geoJson;

    public QualityService(DatabaseContext db, IGeoJsonService geoJson)
    {
        _db = db;
        _geoJson = geoJson;
    }

    public async Task<IReadOnlyList<QualityStandardDto>> GetStandardsAsync(CancellationToken cancellationToken = default) =>
        await _db.QualityStandards.AsNoTracking().Include(s => s.Limits).OrderBy(s => s.Code)
            .Select(s => new QualityStandardDto(s.Code, s.Name, s.Authority, s.Version, s.ReferenceUrl, s.EffectiveFrom,
                s.Limits.OrderBy(l => l.Parameter).Select(l => new QualityLimitDto(l.Parameter, l.Unit, l.MinValue, l.MaxValue, l.Notes, l.ReferenceSection)).ToList()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SamplingStationDto>> GetStationsAsync(CancellationToken cancellationToken = default) =>
        await _db.SamplingStations.AsNoTracking().Include(s => s.AdminArea).Include(s => s.ServiceZone)
            .OrderBy(s => s.Name)
            .Select(s => new SamplingStationDto(s.Id, s.Code, s.Name, s.WaterSourceType,
                s.Geometry == null ? null : s.Geometry.X, s.Geometry == null ? null : s.Geometry.Y,
                s.AdminArea == null ? null : s.AdminArea.Name,
                s.ServiceZone == null ? null : s.ServiceZone.Name,
                s.IsActive, s.DataClassification,
                s.Samples.OrderByDescending(x => x.SampledAt).Select(x => (DateTimeOffset?)x.SampledAt).FirstOrDefault(),
                s.Samples.Count))
            .ToListAsync(cancellationToken);

    public async Task<SamplingStationDto> CreateStationAsync(SamplingStationCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _db.SamplingStations.AnyAsync(s => s.Code == request.Code, cancellationToken))
        {
            throw new InvalidOperationException($"A sampling station with code '{request.Code}' already exists.");
        }
        if (request.Latitude is null || request.Longitude is null)
        {
            throw new ArgumentException("Sampling stations require a latitude and longitude.");
        }
        var station = new SamplingStation
        {
            Code = request.Code.Trim(), Name = request.Name.Trim(),
            WaterSourceType = request.WaterSourceType,
            Geometry = SpatialMath.Point(request.Longitude.Value, request.Latitude.Value),
            AdminAreaId = request.AdminAreaId, ServiceZoneId = request.ServiceZoneId,
            Description = request.Description, IsActive = request.IsActive,
            DataClassification = DataClassification.Observed
        };
        _db.SamplingStations.Add(station);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetStationsAsync(cancellationToken)).First(s => s.Id == station.Id);
    }

    public async Task<IReadOnlyList<SamplingCampaignDto>> GetCampaignsAsync(CancellationToken cancellationToken = default) =>
        await _db.SamplingCampaigns.AsNoTracking().OrderByDescending(c => c.StartedOn)
            .Select(c => new SamplingCampaignDto(c.Id, c.Code, c.Name, c.Status, c.StartedOn, c.EndedOn,
                c.Purpose, c.LaboratoryName, c.LaboratoryAccredited, c.Samples.Count))
            .ToListAsync(cancellationToken);

    public async Task<SamplingCampaignDto> CreateCampaignAsync(CampaignCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _db.SamplingCampaigns.AnyAsync(c => c.Code == request.Code, cancellationToken))
        {
            throw new InvalidOperationException($"A campaign with code '{request.Code}' already exists.");
        }
        var campaign = new SamplingCampaign
        {
            Code = request.Code.Trim(), Name = request.Name.Trim(), Status = request.Status,
            StartedOn = request.StartedOn, EndedOn = request.EndedOn, Purpose = request.Purpose,
            LaboratoryName = request.LaboratoryName, LaboratoryAccredited = request.LaboratoryAccredited,
            AccreditationReference = request.AccreditationReference,
            DataClassification = DataClassification.Observed
        };
        _db.SamplingCampaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);
        return new SamplingCampaignDto(campaign.Id, campaign.Code, campaign.Name, campaign.Status,
            campaign.StartedOn, campaign.EndedOn, campaign.Purpose, campaign.LaboratoryName,
            campaign.LaboratoryAccredited, 0);
    }

    public async Task<PagedResult<WaterSampleDto>> GetSamplesAsync(QualityQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var samples = _db.WaterSamples.AsNoTracking()
            .Include(s => s.Station).Include(s => s.Campaign).Include(s => s.Results).ThenInclude(r => r.AppliedLimit)
            .AsQueryable();
        if (query.StationId is not null)
        {
            samples = samples.Where(s => s.StationId == query.StationId);
        }
        if (query.CampaignId is not null)
        {
            samples = samples.Where(s => s.CampaignId == query.CampaignId);
        }
        if (query.From is not null)
        {
            var from = query.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            samples = samples.Where(s => s.SampledAt >= from);
        }
        if (query.To is not null)
        {
            var to = query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            samples = samples.Where(s => s.SampledAt < to);
        }
        var total = await samples.CountAsync(cancellationToken);
        var items = await samples.OrderByDescending(s => s.SampledAt).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<WaterSampleDto>(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<WaterSampleDto?> GetSampleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sample = await _db.WaterSamples.AsNoTracking()
            .Include(s => s.Station).Include(s => s.Campaign).Include(s => s.Results).ThenInclude(r => r.AppliedLimit)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return sample is null ? null : ToDto(sample);
    }

    public async Task<WaterSampleDto> CreateSampleAsync(SampleCreateRequest request, string userName, CancellationToken cancellationToken = default)
    {
        if (await _db.WaterSamples.AnyAsync(s => s.SampleCode == request.SampleCode, cancellationToken))
        {
            throw new InvalidOperationException($"A sample with code '{request.SampleCode}' already exists.");
        }
        var station = await _db.SamplingStations.FirstOrDefaultAsync(s => s.Id == request.StationId, cancellationToken)
            ?? throw new KeyNotFoundException("Sampling station not found.");
        var campaign = await _db.SamplingCampaigns.FirstOrDefaultAsync(c => c.Id == request.CampaignId, cancellationToken)
            ?? throw new KeyNotFoundException("Sampling campaign not found.");
        var sample = new WaterSample
        {
            SampleCode = request.SampleCode.Trim(), StationId = request.StationId, CampaignId = request.CampaignId,
            SampledAt = request.SampledAt, SampledBy = request.SampledBy ?? userName, ReceivedAt = request.ReceivedAt,
            Notes = request.Notes, DataClassification = request.DataClassification
        };
        foreach (var entry in request.Results)
        {
            sample.Results.Add(await BuildResultAsync(entry, request.DataClassification, cancellationToken));
        }
        _db.WaterSamples.Add(sample);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetSampleAsync(sample.Id, cancellationToken))!;
    }

    public async Task<SampleResultDto> AddResultAsync(Guid sampleId, SampleResultEntry entry, CancellationToken cancellationToken = default)
    {
        var sample = await _db.WaterSamples.Include(s => s.Results)
            .FirstOrDefaultAsync(s => s.Id == sampleId, cancellationToken)
            ?? throw new KeyNotFoundException("Sample not found.");
        if (sample.Results.Any(r => r.Parameter == entry.Parameter))
        {
            throw new InvalidOperationException($"A result for {entry.Parameter} already exists on this sample.");
        }
        var result = await BuildResultAsync(entry, sample.DataClassification, cancellationToken);
        result.SampleId = sampleId;
        _db.SampleResults.Add(result);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResultDto(result);
    }

    public async Task<QualitySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var stations = await _db.SamplingStations.CountAsync(cancellationToken);
        var campaigns = await _db.SamplingCampaigns.CountAsync(cancellationToken);
        var samples = await _db.WaterSamples.CountAsync(cancellationToken);
        var states = await _db.SampleResults.AsNoTracking().GroupBy(r => r.State)
            .Select(g => new { State = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var lastSample = await _db.WaterSamples.AsNoTracking().OrderByDescending(s => s.SampledAt)
            .Select(s => (DateTimeOffset?)s.SampledAt).FirstOrDefaultAsync(cancellationToken);
        var message = samples == 0
            ? "No laboratory samples have been recorded. Water quality is not assessed."
            : "Classification uses the configured standard limits. Missing results are shown as insufficient evidence, not as compliant.";
        return new QualitySummaryDto(stations, campaigns, samples, states.Sum(s => s.Count),
            states.Where(s => s.State == QualityState.Compliant).Sum(s => s.Count),
            states.Where(s => s.State == QualityState.NonCompliant).Sum(s => s.Count),
            states.Where(s => s.State == QualityState.InsufficientEvidence).Sum(s => s.Count),
            states.Where(s => s.State == QualityState.NotAssessed).Sum(s => s.Count),
            lastSample, message);
    }

    public async Task<IReadOnlyList<QualityExceedanceDto>> GetExceedancesAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = _db.SampleResults.AsNoTracking()
            .Include(r => r.Sample).ThenInclude(s => s.Station).Include(r => r.AppliedLimit)
            .Where(r => r.State == QualityState.NonCompliant);
        if (from is not null)
        {
            var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(r => r.Sample.SampledAt >= start);
        }
        if (to is not null)
        {
            var end = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(r => r.Sample.SampledAt < end);
        }
        var results = await query.OrderByDescending(r => r.Sample.SampledAt).Take(500).ToListAsync(cancellationToken);
        return results.Select(r => new QualityExceedanceDto(r.Id, r.Sample.SampleCode, r.Sample.Station.Name,
            r.Parameter, r.Value, r.Unit, r.AppliedLimit?.MinValue, r.AppliedLimit?.MaxValue,
            r.Sample.SampledAt, r.AssessmentNote, r.Sample.DataClassification)).ToList();
    }

    public async Task<IReadOnlyList<QualityHotspotDto>> GetHotspotsAsync(CancellationToken cancellationToken = default)
    {
        var results = await _db.SampleResults.AsNoTracking()
            .Include(r => r.Sample).ThenInclude(s => s.Station)
            .ToListAsync(cancellationToken);
        return results
            .GroupBy(r => new { r.Sample.StationId, r.Parameter })
            .Select(g =>
            {
                var station = g.First().Sample.Station;
                var latest = g.OrderByDescending(r => r.Sample.SampledAt).First();
                return new QualityHotspotDto(station.Id, station.Name, g.Key.Parameter,
                    station.Geometry?.X, station.Geometry?.Y,
                    g.Count(r => r.State == QualityState.NonCompliant), g.Count(),
                    latest.State.ToString(), latest.DataClassification);
            })
            .Where(r => r.NonCompliantResults > 0)
            .OrderByDescending(r => r.NonCompliantResults)
            .Take(100)
            .ToList();
    }

    public async Task<IReadOnlyList<ResultTrendDto>> GetTrendAsync(Guid stationId, ParameterCode parameter, CancellationToken cancellationToken = default)
    {
        var rows = await _db.SampleResults.AsNoTracking()
            .Include(r => r.Sample).Include(r => r.AppliedLimit)
            .Where(r => r.Sample.StationId == stationId && r.Parameter == parameter)
            .OrderBy(r => r.Sample.SampledAt)
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => new { r.Sample.SampledAt.Year, r.Sample.SampledAt.Month })
            .Select(g =>
            {
                var values = g.Where(r => r.Value != null).Select(r => r.Value!.Value).ToList();
                var latest = g.OrderByDescending(r => r.Sample.SampledAt).First();
                return new ResultTrendDto($"{g.Key.Year:D4}-{g.Key.Month:D2}",
                    values.Count == 0 ? null : Math.Round(values.Average(), 3),
                    latest.AppliedLimit?.MaxValue, latest.Unit, latest.State);
            })
            .OrderBy(t => t.Period).ToList();
    }

    public async Task<string> GetChoroplethAsync(CancellationToken cancellationToken = default)
    {
        var stations = await _db.SamplingStations.AsNoTracking()
            .Include(s => s.Samples).ThenInclude(s => s.Results)
            .Where(s => s.Geometry != null).ToListAsync(cancellationToken);
        var features = new List<GeoJsonFeatureOutput>();
        foreach (var station in stations)
        {
            var latest = station.Samples.OrderByDescending(s => s.SampledAt).FirstOrDefault();
            var worst = latest?.Results
                .OrderByDescending(r => r.State == QualityState.NonCompliant ? 3 :
                    r.State == QualityState.InsufficientEvidence ? 2 :
                    r.State == QualityState.NotAssessed ? 1 : 0)
                .FirstOrDefault()?.State.ToString() ?? QualityState.NotAssessed.ToString();
            features.Add(new GeoJsonFeatureOutput(station.Id.ToString(), station.Geometry!, new Dictionary<string, object?>
            {
                ["id"] = station.Id, ["code"] = station.Code, ["name"] = station.Name,
                ["waterSourceType"] = station.WaterSourceType.ToString(),
                ["latestState"] = worst, ["lastSampledAt"] = latest?.SampledAt,
                ["nonCompliantCount"] = latest?.Results.Count(r => r.State == QualityState.NonCompliant) ?? 0,
                ["classification"] = station.DataClassification.ToString()
            }));
        }
        return _geoJson.SerializeFeatureCollection(features);
    }

    private async Task<SampleResult> BuildResultAsync(SampleResultEntry entry, DataClassification classification, CancellationToken cancellationToken)
    {
        var standardCode = string.IsNullOrWhiteSpace(entry.StandardCode) ? "NSDWQ-2007" : entry.StandardCode;
        var standard = await _db.QualityStandards.AsNoTracking().Include(s => s.Limits)
            .FirstOrDefaultAsync(s => s.Code == standardCode, cancellationToken);
        var limit = standard?.Limits.FirstOrDefault(l => l.Parameter == entry.Parameter);
        var (state, note) = Assess(entry.Value, entry.Unit, limit);
        return new SampleResult
        {
            Parameter = entry.Parameter,
            Value = entry.Value,
            Unit = entry.Unit,
            DetectionLimit = entry.DetectionLimit,
            Method = entry.Method,
            Qualifier = entry.Qualifier,
            State = state,
            AppliedLimitId = limit?.Id,
            StandardCode = limit is null ? null : standard?.Code,
            AssessmentNote = note,
            AssessedAt = DateTimeOffset.UtcNow,
            DataClassification = classification
        };
    }

    public static (QualityState State, string Note) Assess(decimal? value, string unit, QualityLimit? limit)
    {
        if (limit is null)
        {
            return (QualityState.NotAssessed, "No applicable limit is configured for this parameter in the selected standard.");
        }
        if (!string.Equals(unit.Trim(), limit.Unit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (QualityState.NotAssessed, $"Result unit '{unit}' does not match the configured limit unit '{limit.Unit}'.");
        }
        if (value is null)
        {
            return (QualityState.InsufficientEvidence, "No numeric result was reported; compliance cannot be evidenced.");
        }
        if (limit.MinValue is not null && value < limit.MinValue)
        {
            return (QualityState.NonCompliant, $"Value {value} is below the minimum limit {limit.MinValue} {limit.Unit}.");
        }
        if (limit.MaxValue is not null && value > limit.MaxValue)
        {
            return (QualityState.NonCompliant, $"Value {value} exceeds the maximum limit {limit.MaxValue} {limit.Unit}.");
        }
        return (QualityState.Compliant, "Value is within the configured limit.");
    }

    private static WaterSampleDto ToDto(WaterSample sample) => new(
        sample.Id, sample.SampleCode, sample.StationId, sample.Station.Name, sample.Campaign.Code,
        sample.SampledAt, sample.SampledBy, sample.Notes, sample.DataClassification,
        sample.Results.OrderBy(r => r.Parameter).Select(ToResultDto).ToList());

    private static SampleResultDto ToResultDto(SampleResult result) => new(
        result.Id, result.Parameter, result.Value, result.Unit, result.DetectionLimit, result.State,
        result.StandardCode, result.AssessmentNote, result.Method,
        result.AppliedLimit?.MinValue, result.AppliedLimit?.MaxValue, result.AppliedLimit?.Unit);
}
