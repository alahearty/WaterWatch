using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/remote-sensing")]
public class RemoteSensingApiController : ControllerBase
{
    private readonly IRemoteSensingService _remoteSensing;

    public RemoteSensingApiController(IRemoteSensingService remoteSensing) => _remoteSensing = remoteSensing;

    [HttpPost("search")]
    public async Task<ActionResult<StacSearchResultDto>> Search([FromBody] StacSearchRequest request, CancellationToken cancellationToken) =>
        Ok(await _remoteSensing.SearchScenesAsync(request, cancellationToken));

    [HttpGet("cached")]
    public async Task<ActionResult<IReadOnlyList<SatelliteSceneDto>>> Cached(CancellationToken cancellationToken) =>
        Ok(await _remoteSensing.GetCachedScenesAsync(cancellationToken));

    [HttpGet("change-layers")]
    public async Task<ActionResult<IReadOnlyList<ChangeLayerDto>>> ChangeLayers(CancellationToken cancellationToken) =>
        Ok(await _remoteSensing.GetChangeLayersAsync(cancellationToken));
}
