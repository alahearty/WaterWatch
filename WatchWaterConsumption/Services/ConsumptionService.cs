using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public static class WaterUnits
{
    public const decimal LitresPerCubicMetre = 1000m;
    public const decimal LitresPerKilolitre = 1000m;

    public static decimal CubicMetresToLitres(decimal cubicMetres) => cubicMetres * LitresPerCubicMetre;
    public static decimal CubicMetresToKilolitres(decimal cubicMetres) => cubicMetres;
    public static decimal LitresToCubicMetres(decimal litres) => litres / LitresPerCubicMetre;
    public static decimal? LitresPerCapitaPerDay(decimal cubicMetres, long? population, double days)
    {
        if (population is null or <= 0 || days <= 0)
        {
            return null;
        }
        return CubicMetresToLitres(cubicMetres) / population.Value / (decimal)days;
    }
}

public interface IConsumptionService
{
    Task<ConsumptionSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsumptionAggregateDto>> GetAggregatesAsync(string dimension, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsumptionTrendDto>> GetTrendAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsumptionAnomalyDto>> GetAnomaliesAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<string> GetChoroplethAsync(string dimension, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<PagedResult<ConsumerAccountDto>> GetAccountsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<MeterDto>> GetMetersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<MeterReadingDto>> GetReadingsAsync(MeterReadingQuery query, CancellationToken cancellationToken = default);
    Task<ReadingImportResultDto> ImportReadingsAsync(Stream stream, string fileName, string userName, bool commit, CancellationToken cancellationToken = default);
    Task<MeterReadingDto> CorrectReadingAsync(Guid readingId, decimal valueM3, string reason, CancellationToken cancellationToken = default);
    Task<ConsumerAccountDto> CreateAccountAsync(ConsumerAccountCreateRequest request, CancellationToken cancellationToken = default);
    Task<MeterDto> CreateMeterAsync(MeterCreateRequest request, CancellationToken cancellationToken = default);
}

public class ConsumptionService : IConsumptionService
{
    private readonly DatabaseContext _db;
    private readonly IGeoJsonService _geoJson;

    public ConsumptionService(DatabaseContext db, IGeoJsonService geoJson)
    {
        _db = db;
        _geoJson = geoJson;
    }

    private sealed record Interval(
        Guid MeterId, Guid? AccountId, DateOnly ReadingDate, decimal ConsumptionM3,
        ReadingType Type, DataClassification Classification, string? Zone, string? Dma,
        string? AdminArea, ConsumerCategory? Category, bool IsEstimated);

    private async Task<List<Interval>> LoadIntervalsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var meters = await _db.Meters.AsNoTracking()
            .Include(m => m.ConsumerAccount).ThenInclude(a => a!.AdminArea)
            .Include(m => m.ConsumerAccount).ThenInclude(a => a!.ServiceZone)
            .Include(m => m.ConsumerAccount).ThenInclude(a => a!.Dma)
            .ToListAsync(cancellationToken);
        var meterMap = meters.ToDictionary(m => m.Id);

        var readingsQuery = _db.MeterReadings.AsNoTracking().AsQueryable();
        if (to is not null)
        {
            readingsQuery = readingsQuery.Where(r => r.ReadingDate <= to);
        }
        var readings = await readingsQuery.OrderBy(r => r.MeterId).ThenBy(r => r.ReadingDate)
            .ToListAsync(cancellationToken);

        var intervals = new List<Interval>();
        DateOnly floor = from ?? new DateOnly(1900, 1, 1);
        foreach (var group in readings.GroupBy(r => r.MeterId))
        {
            if (!meterMap.TryGetValue(group.Key, out var meter))
            {
                continue;
            }
            MeterReading? previous = null;
            foreach (var reading in group.OrderBy(r => r.ReadingDate))
            {
                if (previous is not null && reading.ReadingDate >= floor)
                {
                    decimal consumption;
                    var valid = true;
                    if (reading.ReadingType == ReadingType.Rollover)
                    {
                        var rollover = meter.RolloverValueM3;
                        if (rollover is null || rollover <= previous.ReadingValueM3)
                        {
                            valid = false;
                            consumption = 0;
                        }
                        else
                        {
                            consumption = rollover.Value - previous.ReadingValueM3 + reading.ReadingValueM3;
                        }
                    }
                    else
                    {
                        consumption = reading.ReadingValueM3 - previous.ReadingValueM3;
                        if (consumption < 0)
                        {
                            valid = false;
                        }
                    }

                    if (valid && consumption >= 0)
                    {
                        var account = meter.ConsumerAccount;
                        var classification = reading.DataClassification == DataClassification.SyntheticDemonstration ||
                                             previous.DataClassification == DataClassification.SyntheticDemonstration
                            ? DataClassification.SyntheticDemonstration
                            : reading.ReadingType == ReadingType.Estimated || previous.ReadingType == ReadingType.Estimated
                                ? DataClassification.Estimated
                                : reading.DataClassification;
                        intervals.Add(new Interval(
                            meter.Id, account?.Id, reading.ReadingDate, consumption,
                            reading.ReadingType, classification,
                            account?.ServiceZone?.Name, account?.Dma?.Name,
                            account?.AdminArea?.Name, account?.Category,
                            reading.ReadingType == ReadingType.Estimated || previous.ReadingType == ReadingType.Estimated));
                    }
                }
                previous = reading;
            }
        }
        return intervals;
    }

    public async Task<ConsumptionSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var intervals = await LoadIntervalsAsync(from, to, cancellationToken);
        var readings = await _db.MeterReadings.AsNoTracking().ToListAsync(cancellationToken);
        var meters = await _db.Meters.AsNoTracking().ToListAsync(cancellationToken);
        var accounts = await _db.ConsumerAccounts.AsNoTracking().CountAsync(cancellationToken);
        var total = intervals.Sum(i => i.ConsumptionM3);
        var estimated = intervals.Where(i => i.IsEstimated).Sum(i => i.ConsumptionM3);
        var synthetic = intervals.Any(i => i.Classification == DataClassification.SyntheticDemonstration);
        var coverage = readings.Count == 0
            ? "No meter readings have been imported. Consumption cannot be calculated."
            : intervals.Count == 0
                ? "Readings exist but no interval can be calculated for the selected period."
                : "Consumption is calculated from consecutive cumulative meter readings; estimated and synthetic intervals are labelled.";
        return new ConsumptionSummaryDto(
            from, to, total, WaterUnits.CubicMetresToLitres(total), total,
            accounts, meters.Count, intervals.Count,
            total == 0 ? 0 : Math.Round(estimated / total * 100m, 1),
            await CountAnomaliesAsync(intervals),
            readings.Count == 0 ? null : readings.Min(r => r.ReadingDate),
            readings.Count == 0 ? null : readings.Max(r => r.ReadingDate),
            coverage,
            synthetic ? DataClassification.SyntheticDemonstration : estimated > 0 ? DataClassification.Estimated : DataClassification.Observed);
    }

    private static Task<int> CountAnomaliesAsync(List<Interval> intervals)
    {
        var anomalies = 0;
        foreach (var group in intervals.GroupBy(i => i.MeterId))
        {
            var values = group.Select(i => i.ConsumptionM3).OrderBy(v => v).ToList();
            if (values.Count < 3)
            {
                continue;
            }
            var median = values[values.Count / 2];
            anomalies += values.Count(v => median > 0 && v > median * 3m && v > 10m);
        }
        return Task.FromResult(anomalies);
    }

    public async Task<IReadOnlyList<ConsumptionAggregateDto>> GetAggregatesAsync(string dimension, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var intervals = await LoadIntervalsAsync(from, to, cancellationToken);
        var days = Math.Max(1, (to ?? DateOnly.FromDateTime(DateTime.UtcNow)).DayNumber - (from ?? new DateOnly(1900, 1, 1)).DayNumber + 1);
        if (to is null && intervals.Count > 0)
        {
            days = Math.Max(1, intervals.Max(i => i.ReadingDate).DayNumber - intervals.Min(i => i.ReadingDate).DayNumber + 1);
        }

        IEnumerable<IGrouping<string, Interval>> groups = dimension.ToLowerInvariant() switch
        {
            "adminarea" or "lga" => intervals.GroupBy(i => i.AdminArea ?? "Unattributed"),
            "servicezone" or "zone" => intervals.GroupBy(i => i.Zone ?? "Unattributed"),
            "dma" => intervals.GroupBy(i => i.Dma ?? "Unattributed"),
            "category" => intervals.GroupBy(i => i.Category?.ToString() ?? "Unattributed"),
            _ => intervals.GroupBy(i => i.AdminArea ?? "Unattributed")
        };

        var populations = dimension.ToLowerInvariant() switch
        {
            "adminarea" or "lga" => await _db.AdminAreas.AsNoTracking().Where(a => a.Population != null)
                .ToDictionaryAsync(a => a.Name, a => a.Population, cancellationToken),
            "servicezone" or "zone" or "dma" => await _db.OperationalAreas.AsNoTracking().Where(a => a.Population != null)
                .ToDictionaryAsync(a => a.Name, a => a.Population, cancellationToken),
            _ => new Dictionary<string, long?>()
        };

        return groups
            .Select(g =>
            {
                var total = g.Sum(i => i.ConsumptionM3);
                populations.TryGetValue(g.Key, out var population);
                return new ConsumptionAggregateDto(
                    g.Key, null, total, WaterUnits.CubicMetresToLitres(total),
                    g.Select(i => i.AccountId).Where(id => id != null).Distinct().Count(),
                    population,
                    WaterUnits.LitresPerCapitaPerDay(total, population, days),
                    g.Any(i => i.Classification == DataClassification.SyntheticDemonstration)
                        ? DataClassification.SyntheticDemonstration
                        : g.Any(i => i.Classification == DataClassification.Estimated)
                            ? DataClassification.Estimated
                            : DataClassification.Observed);
            })
            .OrderByDescending(a => a.ConsumptionM3)
            .ToList();
    }

    public async Task<IReadOnlyList<ConsumptionTrendDto>> GetTrendAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var intervals = await LoadIntervalsAsync(from, to, cancellationToken);
        return intervals
            .GroupBy(i => new { i.ReadingDate.Year, i.ReadingDate.Month })
            .Select(g => new ConsumptionTrendDto(
                $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                g.Sum(i => i.ConsumptionM3),
                WaterUnits.CubicMetresToLitres(g.Sum(i => i.ConsumptionM3)),
                g.Count(),
                g.Where(i => i.IsEstimated).Sum(i => i.ConsumptionM3),
                g.Any(i => i.Classification == DataClassification.SyntheticDemonstration)
                    ? DataClassification.SyntheticDemonstration
                    : g.Any(i => i.Classification == DataClassification.Estimated)
                        ? DataClassification.Estimated
                        : DataClassification.Observed))
            .OrderBy(t => t.Period)
            .ToList();
    }

    public async Task<IReadOnlyList<ConsumptionAnomalyDto>> GetAnomaliesAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var intervals = await LoadIntervalsAsync(from, to, cancellationToken);
        var meterSerials = await _db.Meters.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.SerialNumber, cancellationToken);
        var accountNumbers = await _db.ConsumerAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.AccountNumber, cancellationToken);
        var results = new List<ConsumptionAnomalyDto>();
        foreach (var group in intervals.GroupBy(i => i.MeterId))
        {
            var ordered = group.OrderBy(i => i.ReadingDate).ToList();
            if (ordered.Count < 3)
            {
                continue;
            }
            var values = ordered.Select(i => i.ConsumptionM3).OrderBy(v => v).ToList();
            var median = values[values.Count / 2];
            decimal? previousValue = null;
            foreach (var interval in ordered)
            {
                if (median > 0 && interval.ConsumptionM3 > median * 3m && interval.ConsumptionM3 > 10m)
                {
                    results.Add(new ConsumptionAnomalyDto(
                        interval.MeterId,
                        meterSerials.GetValueOrDefault(interval.MeterId, "Unknown"),
                        interval.AccountId is null ? null : accountNumbers.GetValueOrDefault(interval.AccountId.Value),
                        interval.Zone, interval.ReadingDate, interval.ConsumptionM3, previousValue,
                        $"Consumption is {interval.ConsumptionM3:0.###} m³ versus a meter median of {median:0.###} m³.",
                        interval.Classification));
                }
                previousValue = interval.ConsumptionM3;
            }
        }
        return results.OrderByDescending(r => r.ReadingDate).Take(100).ToList();
    }

    public async Task<string> GetChoroplethAsync(string dimension, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var intervals = await LoadIntervalsAsync(from, to, cancellationToken);
        var features = new List<GeoJsonFeatureOutput>();
        switch (dimension.ToLowerInvariant())
        {
            case "servicezone":
            case "zone":
            case "dma":
            {
                var type = dimension.Equals("dma", StringComparison.OrdinalIgnoreCase)
                    ? OperationalAreaType.DistrictMeteredArea
                    : OperationalAreaType.ServiceZone;
                var zones = await _db.OperationalAreas.AsNoTracking()
                    .Where(z => z.AreaType == type && z.Geometry != null).ToListAsync(cancellationToken);
                foreach (var zone in zones)
                {
                    var consumption = intervals.Where(i => (type == OperationalAreaType.DistrictMeteredArea ? i.Dma : i.Zone) == zone.Name)
                        .Sum(i => i.ConsumptionM3);
                    features.Add(new GeoJsonFeatureOutput(zone.Id.ToString(), zone.Geometry!, new Dictionary<string, object?>
                    {
                        ["code"] = zone.Code, ["name"] = zone.Name, ["areaType"] = zone.AreaType.ToString(),
                        ["consumptionM3"] = consumption, ["consumptionLitres"] = WaterUnits.CubicMetresToLitres(consumption),
                        ["population"] = zone.Population, ["classification"] = zone.DataClassification.ToString(),
                        ["isOfficial"] = zone.IsOfficial
                    }));
                }
                break;
            }
            default:
            {
                var areas = await _db.AdminAreas.AsNoTracking()
                    .Where(a => a.Level == AdminLevel.Lga && a.Geometry != null).ToListAsync(cancellationToken);
                foreach (var area in areas)
                {
                    var consumption = intervals.Where(i => i.AdminArea == area.Name).Sum(i => i.ConsumptionM3);
                    features.Add(new GeoJsonFeatureOutput(area.Id.ToString(), area.Geometry!, new Dictionary<string, object?>
                    {
                        ["code"] = area.Code, ["name"] = area.Name, ["level"] = area.Level.ToString(),
                        ["consumptionM3"] = consumption, ["consumptionLitres"] = WaterUnits.CubicMetresToLitres(consumption),
                        ["population"] = area.Population, ["classification"] = area.DataClassification.ToString()
                    }));
                }
                break;
            }
        }
        return _geoJson.SerializeFeatureCollection(features);
    }

    public async Task<PagedResult<ConsumerAccountDto>> GetAccountsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.ConsumerAccounts.AsNoTracking()
            .Include(a => a.AdminArea).Include(a => a.ServiceZone).Include(a => a.Dma)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(a => EF.Functions.ILike(a.AccountNumber, pattern) || EF.Functions.ILike(a.Name, pattern));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(a => a.AccountNumber).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ConsumerAccountDto(a.Id, a.AccountNumber, a.Name, a.Category, a.Address,
                a.AdminArea == null ? null : a.AdminArea.Name,
                a.ServiceZone == null ? null : a.ServiceZone.Name,
                a.Dma == null ? null : a.Dma.Name,
                a.Meters.Count, a.IsSensitive, a.DataClassification))
            .ToListAsync(cancellationToken);
        return new PagedResult<ConsumerAccountDto>(items, total, page, pageSize);
    }

    public async Task<PagedResult<MeterDto>> GetMetersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Meters.AsNoTracking().Include(m => m.ConsumerAccount).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(m => EF.Functions.ILike(m.SerialNumber, pattern) ||
                                     (m.ConsumerAccount != null && EF.Functions.ILike(m.ConsumerAccount.AccountNumber, pattern)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(m => m.SerialNumber).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MeterDto(m.Id, m.SerialNumber,
                m.ConsumerAccount == null ? null : m.ConsumerAccount.AccountNumber,
                m.ConsumerAccount == null ? null : m.ConsumerAccount.Name,
                m.Status.ToString(), m.Multiplier, m.Unit, m.InstalledOn,
                m.Readings.OrderByDescending(r => r.ReadingDate).Select(r => (DateOnly?)r.ReadingDate).FirstOrDefault(),
                m.Readings.OrderByDescending(r => r.ReadingDate).Select(r => (decimal?)r.ReadingValueM3).FirstOrDefault(),
                m.DataClassification))
            .ToListAsync(cancellationToken);
        return new PagedResult<MeterDto>(items, total, page, pageSize);
    }

    public async Task<PagedResult<MeterReadingDto>> GetReadingsAsync(MeterReadingQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var readings = _db.MeterReadings.AsNoTracking().Include(r => r.Meter).ThenInclude(m => m.ConsumerAccount).AsQueryable();
        if (query.MeterId is not null)
        {
            readings = readings.Where(r => r.MeterId == query.MeterId);
        }
        if (query.From is not null)
        {
            readings = readings.Where(r => r.ReadingDate >= query.From);
        }
        if (query.To is not null)
        {
            readings = readings.Where(r => r.ReadingDate <= query.To);
        }
        var total = await readings.CountAsync(cancellationToken);
        var items = await readings.OrderByDescending(r => r.ReadingDate).ThenBy(r => r.Meter.SerialNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new MeterReadingDto(r.Id, r.MeterId, r.Meter.SerialNumber,
                r.Meter.ConsumerAccount == null ? null : r.Meter.ConsumerAccount.AccountNumber,
                r.ReadingDate, r.ReadingValueM3, r.ReadingType, r.CorrectionReason, r.Notes, r.DataClassification))
            .ToListAsync(cancellationToken);
        return new PagedResult<MeterReadingDto>(items, total, page, pageSize);
    }

    public async Task<ReadingImportResultDto> ImportReadingsAsync(Stream stream, string fileName, string userName, bool commit, CancellationToken cancellationToken = default)
    {
        var errors = new List<ReadingImportError>();
        var candidates = new List<(Meter Meter, DateOnly Date, decimal Value, ReadingType Type, string? Notes, DataClassification Classification)>();
        var meters = await _db.Meters.AsNoTracking().ToDictionaryAsync(m => m.SerialNumber.Trim().ToLowerInvariant(), cancellationToken);
        var existing = await _db.MeterReadings.AsNoTracking()
            .Select(r => new { r.MeterId, r.ReadingDate })
            .ToListAsync(cancellationToken);
        var existingKeys = existing.Select(e => (e.MeterId, e.ReadingDate)).ToHashSet();
        var fileKeys = new HashSet<(Guid, DateOnly)>();
        var row = 1;

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null
        });

        if (!await csv.ReadAsync())
        {
            return new ReadingImportResultDto(Guid.Empty, "Failed", 0, 0, 1,
                new[] { new ReadingImportError(0, "The CSV file is empty.") }, null, null);
        }
        csv.ReadHeader();
        var header = csv.HeaderRecord ?? Array.Empty<string>();
        if (!header.Any(h => h.Equals("meter_serial", StringComparison.OrdinalIgnoreCase)) ||
            !header.Any(h => h.Equals("reading_date", StringComparison.OrdinalIgnoreCase)) ||
            !header.Any(h => h.Equals("reading_m3", StringComparison.OrdinalIgnoreCase)))
        {
            return new ReadingImportResultDto(Guid.Empty, "Failed", 0, 0, 1,
                new[] { new ReadingImportError(0, "Required columns: meter_serial, reading_date, reading_m3. Optional: reading_type, notes, classification.") }, null, null);
        }

        while (await csv.ReadAsync())
        {
            row++;
            try
            {
                var serial = csv.GetField("meter_serial")?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(serial) || !meters.TryGetValue(serial, out var meter))
                {
                    errors.Add(new ReadingImportError(row, $"Unknown or missing meter_serial '{serial}'."));
                    continue;
                }
                if (!DateOnly.TryParseExact(csv.GetField("reading_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    errors.Add(new ReadingImportError(row, "reading_date must be yyyy-MM-dd."));
                    continue;
                }
                if (!decimal.TryParse(csv.GetField("reading_m3"), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
                {
                    errors.Add(new ReadingImportError(row, "reading_m3 must be a non-negative number."));
                    continue;
                }

                var typeText = csv.GetField("reading_type");
                var type = string.IsNullOrWhiteSpace(typeText)
                    ? ReadingType.Observed
                    : Enum.TryParse<ReadingType>(typeText, true, out var parsedType) ? parsedType : ReadingType.Observed;
                var notes = csv.GetField("notes");
                var classText = csv.GetField("classification");
                var classification = !string.IsNullOrWhiteSpace(classText) &&
                                     classText.Contains("synthetic", StringComparison.OrdinalIgnoreCase)
                    ? DataClassification.SyntheticDemonstration
                    : type == ReadingType.Estimated ? DataClassification.Estimated : DataClassification.Observed;

                if (existingKeys.Contains((meter.Id, date)) || !fileKeys.Add((meter.Id, date)))
                {
                    errors.Add(new ReadingImportError(row, $"A reading for meter {meter.SerialNumber} on {date:yyyy-MM-dd} already exists or is duplicated in the file."));
                    continue;
                }
                candidates.Add((meter, date, value, type, notes, classification));
            }
            catch (Exception ex)
            {
                errors.Add(new ReadingImportError(row, ex.Message));
            }
        }

        // Validate sequencing per meter using accepted rows and existing history.
        foreach (var group in candidates.GroupBy(c => c.Meter.Id))
        {
            var previous = await _db.MeterReadings.AsNoTracking()
                .Where(r => r.MeterId == group.Key)
                .OrderByDescending(r => r.ReadingDate)
                .FirstOrDefaultAsync(cancellationToken);
            var readings = await _db.MeterReadings.AsNoTracking()
                .Where(r => r.MeterId == group.Key)
                .Select(r => new { r.ReadingDate, r.ReadingValueM3 })
                .ToListAsync(cancellationToken);
            var all = readings.Select(r => (r.ReadingDate, r.ReadingValueM3)).ToList();
            foreach (var candidate in group.OrderBy(c => c.Date))
            {
                var prior = all.Where(r => r.ReadingDate < candidate.Date).OrderByDescending(r => r.ReadingDate).FirstOrDefault();
                if (prior != default && candidate.Value < prior.ReadingValueM3 && candidate.Type != ReadingType.Rollover)
                {
                    errors.Add(new ReadingImportError(0, $"Meter {candidate.Meter.SerialNumber} reading at {candidate.Date:yyyy-MM-dd} is lower than the previous cumulative reading ({prior.ReadingValueM3}). Mark rollover explicitly or correct the value."));
                }
                if (candidate.Type == ReadingType.Rollover && candidate.Meter.RolloverValueM3 is null)
                {
                    errors.Add(new ReadingImportError(0, $"Meter {candidate.Meter.SerialNumber} has no documented rollover value; the rollover cannot be validated."));
                }
                all.Add((candidate.Date, candidate.Value));
            }
        }

        var accepted = candidates.Where(c => errors.All(e => !e.Message.Contains(c.Meter.SerialNumber, StringComparison.Ordinal) ||
                                                          !e.Message.Contains(c.Date.ToString("yyyy-MM-dd"), StringComparison.Ordinal))).ToList();
        // If any sequencing error exists for a meter, do not commit that meter's ambiguous rows.
        var invalidMeters = errors
            .Where(e => e.Message.Contains("Meter ", StringComparison.Ordinal))
            .Select(e => e.Message)
            .ToHashSet();
        accepted = accepted.Where(c => !invalidMeters.Any(m => m.Contains(c.Meter.SerialNumber, StringComparison.Ordinal))).ToList();
        var rejected = row - 1 - accepted.Count;

        var batch = new ConsumptionImport
        {
            FileName = fileName,
            RequestedBy = userName,
            Status = commit ? (errors.Count == 0 ? ImportStatus.Imported : accepted.Count > 0 ? ImportStatus.PartiallyImported : ImportStatus.Failed)
                            : ImportStatus.Preview,
            RowsTotal = row - 1,
            RowsAccepted = accepted.Count,
            RowsRejected = rejected,
            ErrorsJson = errors.Count == 0 ? null : JsonSerializer.Serialize(errors),
            PeriodStart = accepted.Count == 0 ? null : accepted.Min(a => a.Date),
            PeriodEnd = accepted.Count == 0 ? null : accepted.Max(a => a.Date),
            Notes = commit ? "Committed by operator." : "Preview only; no readings written.",
            DataClassification = accepted.Any(a => a.Classification == DataClassification.SyntheticDemonstration)
                ? DataClassification.SyntheticDemonstration
                : DataClassification.Observed
        };
        _db.ConsumptionImports.Add(batch);

        if (commit && accepted.Count > 0)
        {
            foreach (var (meter, date, value, type, notes, classification) in accepted)
            {
                _db.MeterReadings.Add(new MeterReading
                {
                    MeterId = meter.Id,
                    ReadingDate = date,
                    ReadingValueM3 = value,
                    ReadingType = type,
                    Notes = notes,
                    ImportBatch = batch,
                    DataClassification = classification,
                    ValidatedAt = DateTimeOffset.UtcNow,
                    ValidatedBy = userName
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ReadingImportResultDto(batch.Id, batch.Status.ToString(), batch.RowsTotal, batch.RowsAccepted,
            batch.RowsRejected, errors.Take(200).ToList(), batch.PeriodStart, batch.PeriodEnd);
    }

    public async Task<ConsumerAccountDto> CreateAccountAsync(ConsumerAccountCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _db.ConsumerAccounts.AnyAsync(a => a.AccountNumber == request.AccountNumber, cancellationToken))
        {
            throw new InvalidOperationException($"An account with number '{request.AccountNumber}' already exists.");
        }
        if (request.ServiceZoneId is not null && !await _db.OperationalAreas.AnyAsync(a => a.Id == request.ServiceZoneId, cancellationToken))
        {
            throw new KeyNotFoundException("Service zone not found.");
        }
        if (request.DmaId is not null && !await _db.OperationalAreas.AnyAsync(a => a.Id == request.DmaId, cancellationToken))
        {
            throw new KeyNotFoundException("DMA not found.");
        }
        var account = new ConsumerAccount
        {
            AccountNumber = request.AccountNumber.Trim(), Name = request.Name.Trim(), Category = request.Category,
            Address = request.Address, AdminAreaId = request.AdminAreaId, ServiceZoneId = request.ServiceZoneId,
            DmaId = request.DmaId, HouseholdSize = request.HouseholdSize, IsSensitive = request.IsSensitive,
            DataClassification = DataClassification.Observed
        };
        _db.ConsumerAccounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);
        var created = await _db.ConsumerAccounts.AsNoTracking()
            .Include(a => a.AdminArea).Include(a => a.ServiceZone).Include(a => a.Dma)
            .FirstAsync(a => a.Id == account.Id, cancellationToken);
        return new ConsumerAccountDto(created.Id, created.AccountNumber, created.Name, created.Category,
            created.Address, created.AdminArea?.Name, created.ServiceZone?.Name, created.Dma?.Name,
            created.Meters.Count, created.IsSensitive, created.DataClassification);
    }

    public async Task<MeterDto> CreateMeterAsync(MeterCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _db.Meters.AnyAsync(m => m.SerialNumber == request.SerialNumber, cancellationToken))
        {
            throw new InvalidOperationException($"A meter with serial '{request.SerialNumber}' already exists.");
        }
        if (request.ConsumerAccountId is not null && !await _db.ConsumerAccounts.AnyAsync(a => a.Id == request.ConsumerAccountId, cancellationToken))
        {
            throw new KeyNotFoundException("Consumer account not found.");
        }
        if (request.Multiplier <= 0)
        {
            throw new ArgumentException("Meter multiplier must be positive.");
        }
        var meter = new Meter
        {
            SerialNumber = request.SerialNumber.Trim(), ConsumerAccountId = request.ConsumerAccountId,
            AssetId = request.AssetId, InstalledOn = request.InstalledOn, Status = request.Status,
            Multiplier = request.Multiplier, Unit = request.Unit, Manufacturer = request.Manufacturer,
            Model = request.Model, RolloverValueM3 = request.RolloverValueM3,
            DataClassification = DataClassification.Observed
        };
        _db.Meters.Add(meter);
        await _db.SaveChangesAsync(cancellationToken);
        var created = await _db.Meters.AsNoTracking().Include(m => m.ConsumerAccount)
            .Include(m => m.Readings).FirstAsync(m => m.Id == meter.Id, cancellationToken);
        var latest = created.Readings.OrderByDescending(r => r.ReadingDate).FirstOrDefault();
        return new MeterDto(created.Id, created.SerialNumber, created.ConsumerAccount?.AccountNumber,
            created.ConsumerAccount?.Name, created.Status.ToString(), created.Multiplier, created.Unit,
            created.InstalledOn, latest?.ReadingDate, latest?.ReadingValueM3, created.DataClassification);
    }

    public async Task<MeterReadingDto> CorrectReadingAsync(Guid readingId, decimal valueM3, string reason, CancellationToken cancellationToken = default)
    {
        var reading = await _db.MeterReadings.Include(r => r.Meter).ThenInclude(m => m.ConsumerAccount)
            .FirstOrDefaultAsync(r => r.Id == readingId, cancellationToken)
            ?? throw new KeyNotFoundException("Reading not found.");
        if (valueM3 < 0)
        {
            throw new ArgumentException("Corrected reading cannot be negative.");
        }
        reading.CorrectedFromValueM3 = reading.ReadingValueM3;
        reading.ReadingValueM3 = valueM3;
        reading.ReadingType = ReadingType.Corrected;
        reading.CorrectionReason = reason;
        reading.ValidatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new MeterReadingDto(reading.Id, reading.MeterId, reading.Meter.SerialNumber,
            reading.Meter.ConsumerAccount?.AccountNumber, reading.ReadingDate, reading.ReadingValueM3,
            reading.ReadingType, reading.CorrectionReason, reading.Notes, reading.DataClassification);
    }
}
