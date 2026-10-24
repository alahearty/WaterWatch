using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/hydrology")]
public class HydrologyApiController : ControllerBase
{
    private readonly IHydrologyService _hydrology;

    public HydrologyApiController(IHydrologyService hydrology) => _hydrology = hydrology;

    [HttpGet("stations")]
    public async Task<ActionResult<IReadOnlyList<HydrologyStationDto>>> Stations(CancellationToken cancellationToken) =>
        Ok(await _hydrology.GetStationsAsync(cancellationToken));

    [HttpPost("stations")]
    [Authorize(Policy = Policies.CanManageHydrology)]
    public async Task<ActionResult<HydrologyStationDto>> CreateStation([FromBody] HydrologyStationCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _hydrology.CreateStationAsync(request, cancellationToken));

    [HttpGet("observations")]
    public async Task<ActionResult<IReadOnlyList<HydrologyObservationDto>>> Observations([FromQuery] HydrologyQuery query, CancellationToken cancellationToken) =>
        Ok(await _hydrology.GetObservationsAsync(query, cancellationToken));

    [HttpPost("observations")]
    [Authorize(Policy = Policies.CanManageHydrology)]
    public async Task<ActionResult<HydrologyObservationDto>> AddObservation([FromBody] HydrologyObservationCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _hydrology.AddObservationAsync(request, cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<HydrologySummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await _hydrology.GetSummaryAsync(cancellationToken));

    [HttpGet("rainfall/live")]
    public async Task<ActionResult<RainfallLiveDto>> LiveRainfall(CancellationToken cancellationToken) =>
        Ok(await _hydrology.GetLiveRainfallAsync(cancellationToken));

    [HttpPost("rainfall/sync")]
    [Authorize(Policy = Policies.CanManageHydrology)]
    public async Task<ActionResult<object>> SyncRainfall([FromQuery] int days = 30, CancellationToken cancellationToken = default) =>
        Ok(new { imported = await _hydrology.SyncRainfallAsync(days, cancellationToken), source = "Open-Meteo ERA5 archive", note = "Gridded reanalysis; provisional." });

    [HttpGet("obstructions")]
    public async Task<ActionResult<IReadOnlyList<DrainageObstructionDto>>> Obstructions(CancellationToken cancellationToken) =>
        Ok(await _hydrology.GetObstructionsAsync(cancellationToken));

    [HttpPost("obstructions")]
    [Authorize]
    public async Task<ActionResult<DrainageObstructionDto>> CreateObstruction([FromBody] DrainageObstructionCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _hydrology.CreateObstructionAsync(request, User.Identity?.Name ?? "unknown", cancellationToken));
}
