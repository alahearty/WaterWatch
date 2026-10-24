using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface INrwService
{
    Task<IReadOnlyList<DmaDto>> GetDmasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaterBalanceDto>> GetBalancesAsync(Guid? dmaId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<WaterBalanceDto> CreateBalanceAsync(WaterBalanceCreateRequest request, CancellationToken cancellationToken = default);
    Task<NrwSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NrwTrendDto>> GetTrendAsync(Guid? dmaId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeakIncidentDto>> GetLeaksAsync(CancellationToken cancellationToken = default);
    Task<LeakIncidentDto> CreateLeakAsync(LeakIncidentCreateRequest request, string userName, CancellationToken cancellationToken = default);
    Task<LeakIncidentDto> UpdateLeakStatusAsync(Guid id, LeakStatusUpdateRequest request, string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeakHotspotDto>> GetLeakHotspotsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PressureAnomalyDto>> GetPressureAnomaliesAsync(CancellationToken cancellationToken = default);
}

public class NrwService : INrwService
{
    private readonly DatabaseContext _db;

    public NrwService(DatabaseContext db) => _db = db;

    public async Task<IReadOnlyList<DmaDto>> GetDmasAsync(CancellationToken cancellationToken = default)
    {
        var dmas = await _db.OperationalAreas.AsNoTracking()
            .Where(a => a.AreaType == OperationalAreaType.DistrictMeteredArea)
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var balances = await _db.DmaWaterBalances.AsNoTracking()
            .OrderByDescending(b => b.PeriodEnd).ToListAsync(cancellationToken);
        var leaks = await _db.LeakIncidents.AsNoTracking().Where(l => l.DmaId != null)
            .GroupBy(l => l.DmaId!.Value).Select(g => new { DmaId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return dmas.Select(d =>
        {
            var dmaBalances = balances.Where(b => b.DmaId == d.Id).ToList();
            var latest = dmaBalances.FirstOrDefault();
            return new DmaDto(d.Id, d.Code, d.Name, d.AreaSqKm, d.IsOfficial, d.DataClassification,
                leaks.FirstOrDefault(l => l.DmaId == d.Id)?.Count ?? 0,
                dmaBalances.Count,
                latest is null || latest.InputVolumeM3 == 0 ? null : Math.Round(Nrw(latest) / latest.InputVolumeM3 * 100m, 1),
                latest?.PeriodEnd);
        }).ToList();
    }

    public async Task<IReadOnlyList<WaterBalanceDto>> GetBalancesAsync(Guid? dmaId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = _db.DmaWaterBalances.AsNoTracking().Include(b => b.Dma).AsQueryable();
        if (dmaId is not null)
        {
            query = query.Where(b => b.DmaId == dmaId);
        }
        if (from is not null)
        {
            query = query.Where(b => b.PeriodEnd >= from);
        }
        if (to is not null)
        {
            query = query.Where(b => b.PeriodStart <= to);
        }
        var balances = await query.OrderByDescending(b => b.PeriodEnd).ToListAsync(cancellationToken);
        return balances.Select(ToDto).ToList();
    }

    public async Task<WaterBalanceDto> CreateBalanceAsync(WaterBalanceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var dma = await _db.OperationalAreas.FirstOrDefaultAsync(a =>
            a.Id == request.DmaId && a.AreaType == OperationalAreaType.DistrictMeteredArea, cancellationToken)
            ?? throw new KeyNotFoundException("District metered area not found.");
        if (request.PeriodEnd < request.PeriodStart)
        {
            throw new ArgumentException("The period end cannot precede the period start.");
        }
        if (request.InputVolumeM3 < 0 || request.BilledVolumeM3 < 0)
        {
            throw new ArgumentException("Volumes cannot be negative.");
        }
        if (request.BilledVolumeM3 > request.InputVolumeM3)
        {
            throw new ArgumentException("Billed authorised consumption cannot exceed system input; check the meter/billing period alignment.");
        }
        if (await _db.DmaWaterBalances.AnyAsync(b => b.DmaId == request.DmaId && b.PeriodStart == request.PeriodStart && b.PeriodEnd == request.PeriodEnd, cancellationToken))
        {
            throw new InvalidOperationException("A water balance already exists for this DMA and period.");
        }
        var nrw = request.InputVolumeM3 - request.BilledVolumeM3;
        var split = (request.ApparentLossesM3 ?? 0) + (request.RealLossesM3 ?? 0);
        if (split - nrw > 0.001m)
        {
            throw new ArgumentException("Apparent plus real losses cannot exceed non-revenue water.");
        }
        var balance = new DmaWaterBalance
        {
            DmaId = request.DmaId, PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd,
            InputVolumeM3 = request.InputVolumeM3, BilledVolumeM3 = request.BilledVolumeM3,
            UnbilledAuthorisedM3 = request.UnbilledAuthorisedM3, ApparentLossesM3 = request.ApparentLossesM3,
            RealLossesM3 = request.RealLossesM3, MinimumNightFlowM3PerHour = request.MinimumNightFlowM3PerHour,
            Method = request.Method, UncertaintyNotes = request.UncertaintyNotes, Notes = request.Notes,
            DataClassification = request.DataClassification
        };
        _db.DmaWaterBalances.Add(balance);
        await _db.SaveChangesAsync(cancellationToken);
        balance.Dma = dma;
        return ToDto(balance);
    }

    public async Task<NrwSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = _db.DmaWaterBalances.AsNoTracking().AsQueryable();
        if (from is not null) query = query.Where(b => b.PeriodEnd >= from);
        if (to is not null) query = query.Where(b => b.PeriodStart <= to);
        var balances = await query.ToListAsync(cancellationToken);
        var dmas = await _db.OperationalAreas.CountAsync(a => a.AreaType == OperationalAreaType.DistrictMeteredArea, cancellationToken);
        var leaks = await _db.LeakIncidents.AsNoTracking().Where(l => l.Status != LeakStatus.Closed && l.Status != LeakStatus.Verified).ToListAsync(cancellationToken);
        var input = balances.Sum(b => b.InputVolumeM3);
        var billed = balances.Sum(b => b.BilledVolumeM3);
        var nrw = input - billed;
        var apparent = balances.Any(b => b.ApparentLossesM3 != null)
            ? balances.Sum(b => b.ApparentLossesM3 ?? 0)
            : (decimal?)null;
        var real = balances.Any(b => b.RealLossesM3 != null)
            ? balances.Sum(b => b.RealLossesM3 ?? 0)
            : (decimal?)null;
        var unallocated = nrw - (apparent ?? 0) - (real ?? 0);
        var message = balances.Count == 0
            ? "No DMA water balances have been recorded. NRW cannot be calculated without system input and billed consumption."
            : apparent is null || real is null || unallocated > 0
                ? "NRW is calculated, but apparent/real loss components are not fully evidenced for every period; the difference remains unallocated rather than being labelled leakage."
                : "NRW is calculated from documented input and billed volumes; apparent and real losses are shown only where evidenced.";
        return new NrwSummaryDto(dmas, balances.Count, input, billed, nrw,
            input == 0 ? 0 : Math.Round(nrw / input * 100m, 1), apparent, real,
            Math.Max(0, unallocated), leaks.Count, leaks.Sum(l => l.EstimatedLossM3PerHour ?? 0), message);
    }

    public async Task<IReadOnlyList<NrwTrendDto>> GetTrendAsync(Guid? dmaId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = _db.DmaWaterBalances.AsNoTracking().AsQueryable();
        if (dmaId is not null) query = query.Where(b => b.DmaId == dmaId);
        if (from is not null) query = query.Where(b => b.PeriodEnd >= from);
        if (to is not null) query = query.Where(b => b.PeriodStart <= to);
        var balances = await query.ToListAsync(cancellationToken);
        return balances.GroupBy(b => new { b.PeriodEnd.Year, b.PeriodEnd.Month })
            .Select(g =>
            {
                var input = g.Sum(b => b.InputVolumeM3);
                var billed = g.Sum(b => b.BilledVolumeM3);
                var nrw = input - billed;
                return new NrwTrendDto($"{g.Key.Year:D4}-{g.Key.Month:D2}", input, billed, nrw,
                    input == 0 ? 0 : Math.Round(nrw / input * 100m, 1),
                    g.Any(b => b.DataClassification == DataClassification.SyntheticDemonstration)
                        ? DataClassification.SyntheticDemonstration
                        : DataClassification.Observed);
            })
            .OrderBy(t => t.Period).ToList();
    }

    public async Task<IReadOnlyList<LeakIncidentDto>> GetLeaksAsync(CancellationToken cancellationToken = default) =>
        await _db.LeakIncidents.AsNoTracking().Include(l => l.Dma).Include(l => l.Asset)
            .OrderByDescending(l => l.ReportedAt)
            .Select(l => new LeakIncidentDto(l.Id, l.Reference, l.ReportedAt, l.DmaId,
                l.Dma == null ? null : l.Dma.Name, l.AssetId, l.Asset == null ? null : l.Asset.Name,
                l.Geometry == null ? null : l.Geometry.X, l.Geometry == null ? null : l.Geometry.Y,
                l.Description, l.Severity, l.Status, l.EstimatedLossM3PerHour, l.EstimatedLossIsEstimate,
                l.WorkOrderId, l.DataClassification))
            .ToListAsync(cancellationToken);

    public async Task<LeakIncidentDto> CreateLeakAsync(LeakIncidentCreateRequest request, string userName, CancellationToken cancellationToken = default)
    {
        if (request.DmaId is not null && !await _db.OperationalAreas.AnyAsync(a => a.Id == request.DmaId, cancellationToken))
        {
            throw new KeyNotFoundException("DMA not found.");
        }
        var reference = $"LEAK-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var leak = new LeakIncident
        {
            Reference = reference, ReportedAt = DateTimeOffset.UtcNow, DmaId = request.DmaId,
            AssetId = request.AssetId, Description = request.Description,
            Geometry = request.Longitude is not null && request.Latitude is not null
                ? SpatialMath.Point(request.Longitude.Value, request.Latitude.Value) : null,
            Severity = request.Severity, Status = LeakStatus.Reported,
            EstimatedLossM3PerHour = request.EstimatedLossM3PerHour, EstimatedLossIsEstimate = true,
            DataClassification = request.DataClassification
        };
        _db.LeakIncidents.Add(leak);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetLeaksAsync(cancellationToken)).First(l => l.Id == leak.Id);
    }

    public async Task<LeakIncidentDto> UpdateLeakStatusAsync(Guid id, LeakStatusUpdateRequest request, string userName, CancellationToken cancellationToken = default)
    {
        var leak = await _db.LeakIncidents.FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Leak incident not found.");
        var allowed = leak.Status switch
        {
            LeakStatus.Reported => new[] { LeakStatus.Triaged, LeakStatus.Closed },
            LeakStatus.Triaged => new[] { LeakStatus.Assigned, LeakStatus.Closed },
            LeakStatus.Assigned => new[] { LeakStatus.InProgress, LeakStatus.Closed },
            LeakStatus.InProgress => new[] { LeakStatus.Repaired, LeakStatus.Closed },
            LeakStatus.Repaired => new[] { LeakStatus.Verified, LeakStatus.InProgress },
            LeakStatus.Verified => new[] { LeakStatus.Closed },
            _ => Array.Empty<LeakStatus>()
        };
        if (!allowed.Contains(request.Status))
        {
            throw new InvalidOperationException($"Leak status cannot move from {leak.Status} to {request.Status}.");
        }
        leak.Status = request.Status;
        leak.Notes = request.Notes ?? leak.Notes;
        leak.WorkOrderId = request.WorkOrderId ?? leak.WorkOrderId;
        if (request.Status is LeakStatus.Repaired or LeakStatus.Verified or LeakStatus.Closed)
        {
            leak.ResolvedAt ??= DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetLeaksAsync(cancellationToken)).First(l => l.Id == leak.Id);
    }

    public async Task<IReadOnlyList<LeakHotspotDto>> GetLeakHotspotsAsync(CancellationToken cancellationToken = default)
    {
        var leaks = await _db.LeakIncidents.AsNoTracking().Include(l => l.Dma).ToListAsync(cancellationToken);
        return leaks.GroupBy(l => l.DmaId)
            .Select(g =>
            {
                var dma = g.First().Dma;
                var points = g.Where(l => l.Geometry != null).Select(l => l.Geometry!).ToList();
                var longitude = points.Count == 0 ? (double?)null : points.Average(p => p.X);
                var latitude = points.Count == 0 ? (double?)null : points.Average(p => p.Y);
                return new LeakHotspotDto(g.Key, dma?.Name ?? "Unassigned DMA", g.Count(),
                    g.Count(l => l.Status is not (LeakStatus.Closed or LeakStatus.Verified)),
                    g.Sum(l => l.EstimatedLossM3PerHour ?? 0),
                    longitude, latitude,
                    g.Any(l => l.DataClassification == DataClassification.SyntheticDemonstration)
                        ? DataClassification.SyntheticDemonstration
                        : DataClassification.Observed);
            })
            .OrderByDescending(h => h.Incidents).ToList();
    }

    public async Task<IReadOnlyList<PressureAnomalyDto>> GetPressureAnomaliesAsync(CancellationToken cancellationToken = default) =>
        await _db.Alerts.AsNoTracking().Include(a => a.Device).Include(a => a.AlertRule)
            .Where(a => a.Device.DeviceType == DeviceType.Pressure && a.Status != AlertStatus.Resolved)
            .OrderByDescending(a => a.TriggeredAt)
            .Select(a => new PressureAnomalyDto(a.Id, a.Device.Name, a.Device.DeviceCode, a.Value,
                a.TriggeredAt, a.Message, a.Status, a.AlertRule.Severity))
            .ToListAsync(cancellationToken);

    private static decimal Nrw(DmaWaterBalance balance) => balance.InputVolumeM3 - balance.BilledVolumeM3;

    private static WaterBalanceDto ToDto(DmaWaterBalance b)
    {
        var nrw = Nrw(b);
        var splitProvided = b.ApparentLossesM3 is not null && b.RealLossesM3 is not null;
        var partial = b.ApparentLossesM3 is not null || b.RealLossesM3 is not null;
        var unallocated = nrw - (b.ApparentLossesM3 ?? 0) - (b.RealLossesM3 ?? 0);
        return new WaterBalanceDto(b.Id, b.DmaId, b.Dma.Name, b.Dma.Code, b.PeriodStart, b.PeriodEnd,
            b.InputVolumeM3, b.BilledVolumeM3, b.UnbilledAuthorisedM3, b.ApparentLossesM3, b.RealLossesM3,
            nrw, b.InputVolumeM3 == 0 ? 0 : Math.Round(nrw / b.InputVolumeM3 * 100m, 1),
            Math.Max(0, unallocated), splitProvided ? "Evidenced" : partial ? "Partial" : "Unallocated",
            b.MinimumNightFlowM3PerHour, b.Method, b.UncertaintyNotes, b.Notes, b.DataClassification);
    }
}
