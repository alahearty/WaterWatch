using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsApiController : ControllerBase
{
    private readonly DatabaseContext _db;

    public NotificationsApiController(DatabaseContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> Get(CancellationToken cancellationToken)
    {
        var items = new List<NotificationDto>();
        var alerts = await _db.Alerts.AsNoTracking().Include(a => a.AlertRule).Include(a => a.Device)
            .Where(a => a.Status != AlertStatus.Resolved)
            .OrderByDescending(a => a.TriggeredAt).Take(10).ToListAsync(cancellationToken);
        items.AddRange(alerts.Select(a => new NotificationDto("Alert", a.AlertRule.Name,
            $"{a.Device.Name}: {a.Message}", "/IoT", a.TriggeredAt, a.AlertRule.Severity.ToString())));

        var now = DateTimeOffset.UtcNow;
        var overdue = await _db.WorkOrders.AsNoTracking()
            .Where(w => w.DueAt != null && w.DueAt < now &&
                        w.Status != WorkOrderStatus.Closed && w.Status != WorkOrderStatus.Cancelled &&
                        w.Status != WorkOrderStatus.Resolved && w.Status != WorkOrderStatus.Verified)
            .OrderBy(w => w.DueAt).Take(10).ToListAsync(cancellationToken);
        items.AddRange(overdue.Select(w => new NotificationDto("Work order", $"Overdue: {w.Reference}",
            w.Title, "/FieldOperations", w.DueAt ?? now, "High")));

        var nonCompliant = await _db.SampleResults.AsNoTracking()
            .Include(r => r.Sample).ThenInclude(s => s.Station)
            .Where(r => r.State == QualityState.NonCompliant)
            .OrderByDescending(r => r.Sample.SampledAt).Take(10).ToListAsync(cancellationToken);
        items.AddRange(nonCompliant.Select(r => new NotificationDto("Water quality",
            $"{r.Parameter} exceedance at {r.Sample.Station.Name}",
            $"{r.Value} {r.Unit}", "/WaterQuality", r.Sample.SampledAt, "High")));

        return Ok(items.OrderByDescending(i => i.OccurredAt).Take(20).ToList());
    }
}
