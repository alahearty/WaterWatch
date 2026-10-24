using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/consumption")]
public class ConsumptionApiController : ControllerBase
{
    private readonly IConsumptionService _consumption;

    public ConsumptionApiController(IConsumptionService consumption) => _consumption = consumption;

    [HttpGet("summary")]
    public async Task<ActionResult<ConsumptionSummaryDto>> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(await _consumption.GetSummaryAsync(from, to, cancellationToken));

    [HttpGet("aggregates")]
    public async Task<ActionResult<IReadOnlyList<ConsumptionAggregateDto>>> Aggregates(
        [FromQuery] string dimension, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(await _consumption.GetAggregatesAsync(dimension, from, to, cancellationToken));

    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<ConsumptionTrendDto>>> Trend(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(await _consumption.GetTrendAsync(from, to, cancellationToken));

    [HttpGet("anomalies")]
    public async Task<ActionResult<IReadOnlyList<ConsumptionAnomalyDto>>> Anomalies(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(await _consumption.GetAnomaliesAsync(from, to, cancellationToken));

    [HttpGet("choropleth")]
    public async Task<IActionResult> Choropleth(
        [FromQuery] string dimension, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Content(await _consumption.GetChoroplethAsync(dimension, from, to, cancellationToken), "application/json");

    [HttpGet("accounts")]
    public async Task<ActionResult<PagedResult<ConsumerAccountDto>>> Accounts(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _consumption.GetAccountsAsync(search, page, pageSize, cancellationToken));

    [HttpPost("accounts")]
    [Authorize(Policy = Policies.CanImportData)]
    public async Task<ActionResult<ConsumerAccountDto>> CreateAccount([FromBody] ConsumerAccountCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _consumption.CreateAccountAsync(request, cancellationToken));

    [HttpGet("meters")]
    public async Task<ActionResult<PagedResult<MeterDto>>> Meters(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _consumption.GetMetersAsync(search, page, pageSize, cancellationToken));

    [HttpPost("meters")]
    [Authorize(Policy = Policies.CanImportData)]
    public async Task<ActionResult<MeterDto>> CreateMeter([FromBody] MeterCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _consumption.CreateMeterAsync(request, cancellationToken));

    [HttpGet("readings")]
    public async Task<ActionResult<PagedResult<MeterReadingDto>>> Readings([FromQuery] MeterReadingQuery query, CancellationToken cancellationToken) =>
        Ok(await _consumption.GetReadingsAsync(query, cancellationToken));

    [HttpPost("readings/import")]
    [Authorize(Policy = Policies.CanImportData)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ReadingImportResultDto>> ImportReadings(IFormFile file, [FromForm] bool commit, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "A CSV file is required." });
        }
        if (Path.GetExtension(file.FileName).ToLowerInvariant() != ".csv")
        {
            return BadRequest(new { error = "Meter readings must be uploaded as CSV." });
        }
        await using var stream = file.OpenReadStream();
        var result = await _consumption.ImportReadingsAsync(stream, Path.GetFileName(file.FileName),
            User.Identity?.Name ?? "unknown", commit, cancellationToken);
        return Ok(result);
    }

    [HttpPost("readings/{id:guid}/correct")]
    [Authorize(Policy = Policies.CanEditAssets)]
    public async Task<ActionResult<MeterReadingDto>> CorrectReading(Guid id, [FromBody] ReadingCorrectionRequest request, CancellationToken cancellationToken) =>
        Ok(await _consumption.CorrectReadingAsync(id, request.ValueM3, request.Reason, cancellationToken));
}
