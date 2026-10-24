using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/assets")]
public class AssetsApiController : ControllerBase
{
    private readonly IAssetService _assets;

    public AssetsApiController(IAssetService assets) => _assets = assets;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetSummaryDto>>> Get([FromQuery] AssetListQuery query, CancellationToken cancellationToken) =>
        Ok(await _assets.GetAssetsAsync(query, cancellationToken));

    [HttpGet("statistics")]
    public async Task<ActionResult<AssetStatisticsDto>> Statistics(CancellationToken cancellationToken) =>
        Ok(await _assets.GetStatisticsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAssetAsync(id, cancellationToken);
        return asset is null ? NotFound(new { error = "Asset not found or access is restricted." }) : Ok(asset);
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanEditAssets)]
    public async Task<ActionResult<AssetDetailDto>> Create([FromBody] AssetCreateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        var asset = await _assets.CreateAssetAsync(request, User.Identity?.Name ?? "unknown", cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = asset.Id }, asset);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanEditAssets)]
    public async Task<ActionResult<AssetDetailDto>> Update(Guid id, [FromBody] AssetUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        return Ok(await _assets.UpdateAssetAsync(id, request, User.Identity?.Name ?? "unknown", cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanEditAssets)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _assets.DeleteAssetAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/inspections")]
    [Authorize(Policy = Policies.CanManageFieldOperations)]
    public async Task<ActionResult<InspectionDto>> AddInspection(Guid id, [FromBody] InspectionCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _assets.AddInspectionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/maintenance")]
    [Authorize(Policy = Policies.CanManageFieldOperations)]
    public async Task<ActionResult<MaintenanceDto>> AddMaintenance(Guid id, [FromBody] MaintenanceCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _assets.AddMaintenanceAsync(id, request, cancellationToken));
}
