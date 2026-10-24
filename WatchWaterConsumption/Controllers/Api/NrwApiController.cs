using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/nrw")]
public class NrwApiController : ControllerBase
{
    private readonly INrwService _nrw;

    public NrwApiController(INrwService nrw) => _nrw = nrw;

    [HttpGet("dmas")]
    public async Task<ActionResult<IReadOnlyList<DmaDto>>> Dmas(CancellationToken cancellationToken) =>
        Ok(await _nrw.GetDmasAsync(cancellationToken));

    [HttpGet("balances")]
    public async Task<ActionResult<IReadOnlyList<WaterBalanceDto>>> Balances(
        [FromQuery] Guid? dmaId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(await _nrw.GetBalancesAsync(dmaId, from, to, cancellationToken));

    [HttpPost("balances")]
    [Authorize(Policy = Policies.CanManageNrw)]
    public async Task<ActionResult<WaterBalanceDto>> CreateBalance([FromBody] WaterBalanceCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _nrw.CreateBalanceAsync(request, cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<NrwSummaryDto>> Summary(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(await _nrw.GetSummaryAsync(from, to, cancellationToken));

    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<NrwTrendDto>>> Trend(
        [FromQuery] Guid? dmaId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(await _nrw.GetTrendAsync(dmaId, from, to, cancellationToken));

    [HttpGet("leaks")]
    public async Task<ActionResult<IReadOnlyList<LeakIncidentDto>>> Leaks(CancellationToken cancellationToken) =>
        Ok(await _nrw.GetLeaksAsync(cancellationToken));

    [HttpPost("leaks")]
    [Authorize(Policy = Policies.CanManageFieldOperations)]
    public async Task<ActionResult<LeakIncidentDto>> CreateLeak([FromBody] LeakIncidentCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _nrw.CreateLeakAsync(request, User.Identity?.Name ?? "unknown", cancellationToken));

    [HttpPost("leaks/{id:guid}/status")]
    [Authorize(Policy = Policies.CanManageFieldOperations)]
    public async Task<ActionResult<LeakIncidentDto>> UpdateLeakStatus(Guid id, [FromBody] LeakStatusUpdateRequest request, CancellationToken cancellationToken) =>
        Ok(await _nrw.UpdateLeakStatusAsync(id, request, User.Identity?.Name ?? "unknown", cancellationToken));

    [HttpGet("hotspots")]
    public async Task<ActionResult<IReadOnlyList<LeakHotspotDto>>> Hotspots(CancellationToken cancellationToken) =>
        Ok(await _nrw.GetLeakHotspotsAsync(cancellationToken));

    [HttpGet("pressure-anomalies")]
    public async Task<ActionResult<IReadOnlyList<PressureAnomalyDto>>> PressureAnomalies(CancellationToken cancellationToken) =>
        Ok(await _nrw.GetPressureAnomaliesAsync(cancellationToken));
}
