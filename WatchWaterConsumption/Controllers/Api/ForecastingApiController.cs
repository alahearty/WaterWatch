using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/forecasting")]
[Authorize]
public class ForecastingApiController : ControllerBase
{
    private readonly IForecastService _forecast;

    public ForecastingApiController(IForecastService forecast) => _forecast = forecast;

    [HttpGet("demand")]
    public async Task<ActionResult<DemandForecastDto>> Demand([FromQuery] int months = 6, CancellationToken cancellationToken = default) =>
        Ok(await _forecast.GetDemandForecastAsync(months, cancellationToken));

    [HttpGet("water-level")]
    public async Task<ActionResult<WaterLevelForecastDto>> WaterLevel(
        [FromQuery] Guid? boreholeAssetId, CancellationToken cancellationToken = default) =>
        Ok(await _forecast.GetWaterLevelForecastAsync(boreholeAssetId, cancellationToken));

    [HttpGet("asset-risk")]
    public async Task<ActionResult<IReadOnlyList<AssetRiskDto>>> AssetRisk(CancellationToken cancellationToken) =>
        Ok(await _forecast.GetAssetFailureRiskAsync(cancellationToken));

    [HttpGet("leakage-priority")]
    public async Task<ActionResult<IReadOnlyList<LeakagePriorityDto>>> LeakagePriority(CancellationToken cancellationToken) =>
        Ok(await _forecast.GetLeakagePrioritiesAsync(cancellationToken));

    [HttpGet("quality-deterioration")]
    public async Task<ActionResult<IReadOnlyList<QualityDeteriorationDto>>> QualityDeterioration(CancellationToken cancellationToken) =>
        Ok(await _forecast.GetQualityDeteriorationAsync(cancellationToken));

    [HttpGet("overview")]
    public async Task<ActionResult<ForecastOverviewDto>> Overview(CancellationToken cancellationToken) =>
        Ok(await _forecast.GetOverviewAsync(cancellationToken));
}
