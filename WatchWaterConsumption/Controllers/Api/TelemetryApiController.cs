using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
[Authorize(Policy = Policies.CanViewTelemetry)]
public class TelemetryApiController : ControllerBase
{
    private readonly ITelemetryService _telemetry;

    public TelemetryApiController(ITelemetryService telemetry) => _telemetry = telemetry;

    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> Devices(CancellationToken cancellationToken) =>
        Ok(await _telemetry.GetDevicesAsync(cancellationToken));

    [HttpPost("devices")]
    [Authorize(Policy = Policies.CanManageTelemetry)]
    public async Task<ActionResult<DeviceCreatedDto>> CreateDevice([FromBody] DeviceCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _telemetry.CreateDeviceAsync(request, cancellationToken));

    [HttpGet("readings")]
    public async Task<ActionResult<IReadOnlyList<TelemetryReadingDto>>> Readings([FromQuery] TelemetryQuery query, CancellationToken cancellationToken) =>
        Ok(await _telemetry.GetReadingsAsync(query, cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<TelemetrySummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await _telemetry.GetSummaryAsync(cancellationToken));

    [HttpGet("alert-rules")]
    [Authorize(Policy = Policies.CanManageTelemetry)]
    public async Task<ActionResult<IReadOnlyList<AlertRuleDto>>> AlertRules(CancellationToken cancellationToken) =>
        Ok(await _telemetry.GetAlertRulesAsync(cancellationToken));

    [HttpPost("alert-rules")]
    [Authorize(Policy = Policies.CanManageTelemetry)]
    public async Task<ActionResult<AlertRuleDto>> CreateAlertRule([FromBody] AlertRuleCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _telemetry.CreateAlertRuleAsync(request, cancellationToken));

    [HttpGet("alerts")]
    public async Task<ActionResult<IReadOnlyList<AlertDto>>> Alerts(CancellationToken cancellationToken) =>
        Ok(await _telemetry.GetAlertsAsync(cancellationToken));

    [HttpPost("alerts/{id:guid}/acknowledge")]
    [Authorize(Policy = Policies.CanManageTelemetry)]
    public async Task<ActionResult<AlertDto>> Acknowledge(Guid id, [FromBody] AlertAcknowledgeRequest request, CancellationToken cancellationToken) =>
        Ok(await _telemetry.AcknowledgeAlertAsync(id, User.Identity?.Name ?? "unknown", request.Notes, cancellationToken));

    [HttpPost("alerts/escalate")]
    [Authorize(Policy = Policies.CanManageTelemetry)]
    public async Task<ActionResult<object>> Escalate(CancellationToken cancellationToken) =>
        Ok(new { escalated = await _telemetry.EscalateDueAlertsAsync(cancellationToken) });
}

[ApiController]
[Route("api/telemetry")]
public class TelemetryIngestController : ControllerBase
{
    private readonly ITelemetryService _telemetry;

    public TelemetryIngestController(ITelemetryService telemetry) => _telemetry = telemetry;

    [HttpPost("ingest")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<TelemetryReadingDto>> Ingest([FromBody] TelemetryIngestRequest request, CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Device-Key", out var key))
        {
            return Unauthorized(new { error = "The X-Device-Key header is required." });
        }
        return Ok(await _telemetry.IngestAsync(request, key.ToString(), cancellationToken));
    }
}
