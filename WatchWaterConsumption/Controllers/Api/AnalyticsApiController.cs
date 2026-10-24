using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsApiController : ControllerBase
{
    private readonly ISpatialAnalysisService _analytics;

    public AnalyticsApiController(ISpatialAnalysisService analytics) => _analytics = analytics;

    [HttpGet("point-in-polygon")]
    public async Task<ActionResult<PointInPolygonResultDto>> PointInPolygon(
        [FromQuery] double lon, [FromQuery] double lat, CancellationToken cancellationToken) =>
        Ok(await _analytics.PointInPolygonAsync(lon, lat, cancellationToken));

    [HttpPost("buffer")]
    public async Task<ActionResult<BufferAnalysisDto>> Buffer([FromBody] BufferRequest request, CancellationToken cancellationToken) =>
        Ok(await _analytics.BufferAsync(request, cancellationToken));

    [HttpGet("nearest-assets")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<NearestAssetDto>>> Nearest(
        [FromQuery] double lon, [FromQuery] double lat, [FromQuery] int limit = 5, CancellationToken cancellationToken = default) =>
        Ok(await _analytics.NearestAssetsAsync(lon, lat, limit, cancellationToken));

    [HttpGet("coverage")]
    [Authorize]
    public async Task<ActionResult<CoverageResultDto>> Coverage(
        [FromQuery] AssetType? assetType, [FromQuery] double radiusMeters = 500, CancellationToken cancellationToken = default) =>
        Ok(await _analytics.CoverageAsync(assetType, radiusMeters, cancellationToken));

    [HttpGet("density")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<DensityRowDto>>> Density(
        [FromQuery] string dimension = "lga", CancellationToken cancellationToken = default) =>
        Ok(await _analytics.DensityAsync(dimension, cancellationToken));

    [HttpGet("network/nodes")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<NetworkNodeDto>>> Nodes(CancellationToken cancellationToken) =>
        Ok(await _analytics.GetNetworkNodesAsync(cancellationToken));

    [HttpGet("network/edges")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<NetworkEdgeDto>>> Edges(CancellationToken cancellationToken) =>
        Ok(await _analytics.GetNetworkEdgesAsync(cancellationToken));

    [HttpGet("network/trace")]
    [Authorize]
    public async Task<ActionResult<NetworkTraceDto>> Trace(
        [FromQuery] Guid nodeId, [FromQuery] string direction = "downstream",
        [FromQuery] bool includeUnverified = false, CancellationToken cancellationToken = default) =>
        Ok(await _analytics.TraceNetworkAsync(nodeId, direction, includeUnverified, cancellationToken));

    [HttpGet("network/isolation")]
    [Authorize]
    public async Task<ActionResult<IsolationAnalysisDto>> Isolation(
        [FromQuery] Guid edgeId, CancellationToken cancellationToken = default) =>
        Ok(await _analytics.IsolateEdgeAsync(edgeId, cancellationToken));
}
