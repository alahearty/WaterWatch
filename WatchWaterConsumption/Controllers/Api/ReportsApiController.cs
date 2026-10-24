using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsApiController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsApiController(IReportService reports) => _reports = reports;

    [HttpGet("types")]
    public ActionResult<IReadOnlyList<ReportTypeDto>> Types() => Ok(_reports.GetReportTypes());

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<ReportJobDto>>> Jobs(CancellationToken cancellationToken) =>
        Ok(await _reports.GetJobsAsync(cancellationToken));

    [HttpGet("{reportType}")]
    public async Task<IActionResult> Generate(string reportType, [FromQuery] ReportFormat format = ReportFormat.Csv,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? adminAreaId = null, [FromQuery] Guid? operationalAreaId = null,
        [FromQuery] string? search = null, [FromQuery] string? dimension = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ReportRequest(reportType, format, from, to, adminAreaId, operationalAreaId, search, dimension);
        var file = await _reports.GenerateAsync(request, User.Identity?.Name ?? "unknown", cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
