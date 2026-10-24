using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/flood")]
public class FloodApiController : ControllerBase
{
    private readonly IFloodService _flood;

    public FloodApiController(IFloodService flood) => _flood = flood;

    [HttpGet("layers")]
    public async Task<ActionResult<IReadOnlyList<FloodLayerDto>>> Layers(CancellationToken cancellationToken) =>
        Ok(await _flood.GetLayersAsync(cancellationToken));

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<FloodEventDto>>> Events(CancellationToken cancellationToken) =>
        Ok(await _flood.GetEventsAsync(cancellationToken));

    [HttpPost("layers")]
    [Authorize(Policy = Policies.CanManageHydrology)]
    public async Task<ActionResult<FloodLayerDto>> CreateLayer([FromBody] FloodLayerCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _flood.CreateLayerAsync(request, cancellationToken));

    [HttpPost("events")]
    [Authorize(Policy = Policies.CanManageHydrology)]
    public async Task<ActionResult<FloodEventDto>> CreateEvent([FromBody] FloodEventCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _flood.CreateEventAsync(request, cancellationToken));

    [HttpPost("layers/{id:guid}/analyse")]
    [Authorize(Policy = Policies.CanManageHydrology)]
    public async Task<ActionResult<FloodExposureDto>> Analyse(Guid id, CancellationToken cancellationToken) =>
        Ok(await _flood.AnalyseExposureAsync(id, User.Identity?.Name ?? "unknown", cancellationToken));

    [HttpGet("exposures")]
    public async Task<ActionResult<IReadOnlyList<FloodExposureDto>>> Exposures(CancellationToken cancellationToken) =>
        Ok(await _flood.GetExposureResultsAsync(cancellationToken));
}
