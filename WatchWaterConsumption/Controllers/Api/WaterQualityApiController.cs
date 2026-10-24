using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/water-quality")]
public class WaterQualityApiController : ControllerBase
{
    private readonly IQualityService _quality;

    public WaterQualityApiController(IQualityService quality) => _quality = quality;

    [HttpGet("standards")]
    public async Task<ActionResult<IReadOnlyList<QualityStandardDto>>> Standards(CancellationToken cancellationToken) =>
        Ok(await _quality.GetStandardsAsync(cancellationToken));

    [HttpGet("stations")]
    public async Task<ActionResult<IReadOnlyList<SamplingStationDto>>> Stations(CancellationToken cancellationToken) =>
        Ok(await _quality.GetStationsAsync(cancellationToken));

    [HttpPost("stations")]
    [Authorize(Policy = Policies.CanManageQuality)]
    public async Task<ActionResult<SamplingStationDto>> CreateStation([FromBody] SamplingStationCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _quality.CreateStationAsync(request, cancellationToken));

    [HttpGet("campaigns")]
    public async Task<ActionResult<IReadOnlyList<SamplingCampaignDto>>> Campaigns(CancellationToken cancellationToken) =>
        Ok(await _quality.GetCampaignsAsync(cancellationToken));

    [HttpPost("campaigns")]
    [Authorize(Policy = Policies.CanManageQuality)]
    public async Task<ActionResult<SamplingCampaignDto>> CreateCampaign([FromBody] CampaignCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _quality.CreateCampaignAsync(request, cancellationToken));

    [HttpGet("samples")]
    public async Task<ActionResult<PagedResult<WaterSampleDto>>> Samples([FromQuery] QualityQuery query, CancellationToken cancellationToken) =>
        Ok(await _quality.GetSamplesAsync(query, cancellationToken));

    [HttpGet("samples/{id:guid}")]
    public async Task<ActionResult<WaterSampleDto>> Sample(Guid id, CancellationToken cancellationToken)
    {
        var sample = await _quality.GetSampleAsync(id, cancellationToken);
        return sample is null ? NotFound() : Ok(sample);
    }

    [HttpPost("samples")]
    [Authorize(Policy = Policies.CanManageQuality)]
    public async Task<ActionResult<WaterSampleDto>> CreateSample([FromBody] SampleCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _quality.CreateSampleAsync(request, User.Identity?.Name ?? "unknown", cancellationToken));

    [HttpPost("samples/{id:guid}/results")]
    [Authorize(Policy = Policies.CanManageQuality)]
    public async Task<ActionResult<SampleResultDto>> AddResult(Guid id, [FromBody] SampleResultEntry request, CancellationToken cancellationToken) =>
        Ok(await _quality.AddResultAsync(id, request, cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<QualitySummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await _quality.GetSummaryAsync(cancellationToken));

    [HttpGet("exceedances")]
    public async Task<ActionResult<IReadOnlyList<QualityExceedanceDto>>> Exceedances(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(await _quality.GetExceedancesAsync(from, to, cancellationToken));

    [HttpGet("hotspots")]
    public async Task<ActionResult<IReadOnlyList<QualityHotspotDto>>> Hotspots(CancellationToken cancellationToken) =>
        Ok(await _quality.GetHotspotsAsync(cancellationToken));

    [HttpGet("stations/{stationId:guid}/trend")]
    public async Task<ActionResult<IReadOnlyList<ResultTrendDto>>> Trend(Guid stationId, [FromQuery] ParameterCode parameter, CancellationToken cancellationToken) =>
        Ok(await _quality.GetTrendAsync(stationId, parameter, cancellationToken));

    [HttpGet("choropleth")]
    public async Task<IActionResult> Choropleth(CancellationToken cancellationToken) =>
        Content(await _quality.GetChoroplethAsync(cancellationToken), "application/json");
}
