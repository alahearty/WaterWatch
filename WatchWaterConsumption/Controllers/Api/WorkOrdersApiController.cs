using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/work-orders")]
[Authorize]
public class WorkOrdersApiController : ControllerBase
{
    private readonly IWorkOrderService _workOrders;

    public WorkOrdersApiController(IWorkOrderService workOrders) => _workOrders = workOrders;

    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkOrderDto>>> Get([FromQuery] WorkOrderQuery query, CancellationToken cancellationToken) =>
        Ok(await _workOrders.GetWorkOrdersAsync(query, cancellationToken));

    [HttpGet("statistics")]
    public async Task<ActionResult<WorkOrderStatsDto>> Statistics(CancellationToken cancellationToken) =>
        Ok(await _workOrders.GetStatsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _workOrders.GetWorkOrderAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanManageFieldOperations)]
    public async Task<ActionResult<WorkOrderDetailDto>> Create([FromBody] WorkOrderCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _workOrders.CreateAsync(request, User.Identity?.Name ?? "unknown", cancellationToken));

    [HttpPost("{id:guid}/transition")]
    [Authorize(Policy = Policies.CanManageFieldOperations)]
    public async Task<ActionResult<WorkOrderDetailDto>> Transition(Guid id, [FromBody] WorkOrderTransitionRequest request, CancellationToken cancellationToken) =>
        Ok(await _workOrders.TransitionAsync(id, request, User.Identity?.Name ?? "unknown", cancellationToken));
}
