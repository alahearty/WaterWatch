using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/groundwater")]
public class GroundwaterApiController : ControllerBase
{
    private readonly IGroundwaterService _groundwater;

    public GroundwaterApiController(IGroundwaterService groundwater) => _groundwater = groundwater;

    [HttpGet("boreholes")]
    public async Task<ActionResult<IReadOnlyList<BoreholeDto>>> Boreholes(CancellationToken cancellationToken) =>
        Ok(await _groundwater.GetBoreholesAsync(cancellationToken));

    [HttpGet("boreholes/{assetId:guid}")]
    public async Task<ActionResult<BoreholeDetailDto>> Borehole(Guid assetId, CancellationToken cancellationToken)
    {
        var borehole = await _groundwater.GetBoreholeAsync(assetId, cancellationToken);
        return borehole is null ? NotFound() : Ok(borehole);
    }

    [HttpPut("boreholes/{assetId:guid}/details")]
    [Authorize(Policy = Policies.CanEditAssets)]
    public async Task<ActionResult<BoreholeDetailDto>> UpsertDetail(Guid assetId, [FromBody] BoreholeDetailUpsertRequest request, CancellationToken cancellationToken) =>
        Ok(await _groundwater.UpsertDetailAsync(assetId, request, cancellationToken));

    [HttpGet("boreholes/{assetId:guid}/readings")]
    public async Task<ActionResult<IReadOnlyList<GroundwaterReadingDto>>> Readings(Guid assetId, CancellationToken cancellationToken) =>
        Ok(await _groundwater.GetReadingsAsync(assetId, cancellationToken));

    [HttpPost("boreholes/{assetId:guid}/readings")]
    [Authorize(Policy = Policies.CanManageHydrology)]
    public async Task<ActionResult<GroundwaterReadingDto>> AddReading(Guid assetId, [FromBody] GroundwaterReadingCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _groundwater.AddReadingAsync(assetId, request, cancellationToken));

    [HttpGet("boreholes/{assetId:guid}/pumping-tests")]
    public async Task<ActionResult<IReadOnlyList<PumpingTestDto>>> PumpingTests(Guid assetId, CancellationToken cancellationToken) =>
        Ok(await _groundwater.GetPumpingTestsAsync(assetId, cancellationToken));

    [HttpPost("boreholes/{assetId:guid}/pumping-tests")]
    [Authorize(Policy = Policies.CanManageHydrology)]
    public async Task<ActionResult<PumpingTestDto>> AddPumpingTest(Guid assetId, [FromBody] PumpingTestCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _groundwater.AddPumpingTestAsync(assetId, request, cancellationToken));

    [HttpGet("boreholes/{assetId:guid}/trend")]
    public async Task<ActionResult<IReadOnlyList<GroundwaterTrendDto>>> Trend(Guid assetId, CancellationToken cancellationToken) =>
        Ok(await _groundwater.GetTrendAsync(assetId, cancellationToken));

    [HttpGet("boreholes/{assetId:guid}/contamination-proximity")]
    public async Task<ActionResult<IReadOnlyList<ContaminationProximityDto>>> Contamination(Guid assetId,
        [FromQuery] double radiusMeters = 1000, CancellationToken cancellationToken = default) =>
        Ok(await _groundwater.GetNearbyContaminationAsync(assetId, radiusMeters, cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<GroundwaterSummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await _groundwater.GetSummaryAsync(cancellationToken));
}
