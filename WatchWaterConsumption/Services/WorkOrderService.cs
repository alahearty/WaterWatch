using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface IWorkOrderService
{
    Task<PagedResult<WorkOrderDto>> GetWorkOrdersAsync(WorkOrderQuery query, CancellationToken cancellationToken = default);
    Task<WorkOrderDetailDto?> GetWorkOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkOrderDetailDto> CreateAsync(WorkOrderCreateRequest request, string userName, CancellationToken cancellationToken = default);
    Task<WorkOrderDetailDto> TransitionAsync(Guid id, WorkOrderTransitionRequest request, string userName, CancellationToken cancellationToken = default);
    Task<WorkOrderStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}

public class WorkOrderService : IWorkOrderService
{
    private readonly DatabaseContext _db;

    public WorkOrderService(DatabaseContext db) => _db = db;

    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> AllowedTransitions = new()
    {
        [WorkOrderStatus.Reported] = new[] { WorkOrderStatus.Triaged, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.Triaged] = new[] { WorkOrderStatus.Assigned, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.Assigned] = new[] { WorkOrderStatus.InProgress, WorkOrderStatus.Triaged, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.InProgress] = new[] { WorkOrderStatus.Resolved, WorkOrderStatus.Assigned },
        [WorkOrderStatus.Resolved] = new[] { WorkOrderStatus.Verified, WorkOrderStatus.InProgress },
        [WorkOrderStatus.Verified] = new[] { WorkOrderStatus.Closed },
        [WorkOrderStatus.Closed] = Array.Empty<WorkOrderStatus>(),
        [WorkOrderStatus.Cancelled] = Array.Empty<WorkOrderStatus>()
    };

    public async Task<PagedResult<WorkOrderDto>> GetWorkOrdersAsync(WorkOrderQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var orders = _db.WorkOrders.AsNoTracking().Include(w => w.Asset).AsQueryable();
        if (query.Status is not null) orders = orders.Where(w => w.Status == query.Status);
        if (query.Priority is not null) orders = orders.Where(w => w.Priority == query.Priority);
        if (query.Type is not null) orders = orders.Where(w => w.Type == query.Type);
        if (!string.IsNullOrWhiteSpace(query.AssignedTo)) orders = orders.Where(w => w.AssignedTo == query.AssignedTo);
        if (query.AssetId is not null) orders = orders.Where(w => w.AssetId == query.AssetId);
        var total = await orders.CountAsync(cancellationToken);
        var items = await orders.OrderByDescending(w => w.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<WorkOrderDto>(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<WorkOrderDetailDto?> GetWorkOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _db.WorkOrders.AsNoTracking().Include(w => w.Asset).Include(w => w.Events)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        return order is null ? null : new WorkOrderDetailDto(ToDto(order),
            order.Events.OrderBy(e => e.ChangedAt).Select(e => new WorkOrderEventDto(e.Id, e.FromStatus, e.ToStatus, e.ChangedBy, e.ChangedAt, e.Notes)).ToList());
    }

    public async Task<WorkOrderDetailDto> CreateAsync(WorkOrderCreateRequest request, string userName, CancellationToken cancellationToken = default)
    {
        Asset? asset = null;
        if (request.AssetId is not null)
        {
            asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken)
                ?? throw new KeyNotFoundException("Asset not found.");
        }
        var geometry = request.Longitude is not null && request.Latitude is not null
            ? SpatialMath.Point(request.Longitude.Value, request.Latitude.Value)
            : asset?.Geometry?.Centroid;
        var reference = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var order = new WorkOrder
        {
            Reference = reference, Title = request.Title.Trim(), Description = request.Description,
            Type = request.Type, Priority = request.Priority, Status = WorkOrderStatus.Reported,
            AssetId = request.AssetId, Geometry = geometry, ReportedBy = userName,
            AssignedTo = request.AssignedTo, DueAt = request.DueAt, DataClassification = DataClassification.Observed
        };
        order.Events.Add(new WorkOrderEvent
        {
            FromStatus = null, ToStatus = WorkOrderStatus.Reported, ChangedBy = userName,
            ChangedAt = DateTimeOffset.UtcNow, Notes = "Work order reported.",
            DataClassification = DataClassification.Observed
        });
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetWorkOrderAsync(order.Id, cancellationToken))!;
    }

    public async Task<WorkOrderDetailDto> TransitionAsync(Guid id, WorkOrderTransitionRequest request, string userName, CancellationToken cancellationToken = default)
    {
        var order = await _db.WorkOrders.Include(w => w.Events).FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Work order not found.");
        if (!AllowedTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains(request.Status))
        {
            throw new InvalidOperationException($"Work order cannot move from {order.Status} to {request.Status}.");
        }
        if (request.Status == WorkOrderStatus.Assigned)
        {
            if (string.IsNullOrWhiteSpace(request.AssignedTo ?? order.AssignedTo))
            {
                throw new ArgumentException("An assignee is required when assigning a work order.");
            }
            order.AssignedTo = request.AssignedTo ?? order.AssignedTo;
        }
        if (request.Status == WorkOrderStatus.InProgress && string.IsNullOrWhiteSpace(order.AssignedTo))
        {
            throw new ArgumentException("A work order must be assigned before work can start.");
        }
        if (request.Status == WorkOrderStatus.Resolved)
        {
            if (string.IsNullOrWhiteSpace(request.Notes))
            {
                throw new ArgumentException("Resolution notes are required before marking a work order resolved.");
            }
            order.ResolvedAt = DateTimeOffset.UtcNow;
            order.ResolvedBy = userName;
            order.ResolutionNotes = request.Notes;
        }
        if (request.Status == WorkOrderStatus.Verified)
        {
            if (string.IsNullOrWhiteSpace(request.Notes))
            {
                throw new ArgumentException("Verification notes are required.");
            }
            if (string.Equals(order.ResolvedBy, userName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The person who resolved the work order cannot also verify it. Use a different authorised user.");
            }
            order.VerifiedAt = DateTimeOffset.UtcNow;
            order.VerifiedBy = userName;
            order.VerificationNotes = request.Notes;
        }
        if (request.Status == WorkOrderStatus.Closed)
        {
            order.ClosedAt = DateTimeOffset.UtcNow;
            order.ClosedBy = userName;
        }
        if (request.DueAt is not null)
        {
            order.DueAt = request.DueAt;
        }

        var from = order.Status;
        order.Status = request.Status;
        var transitionEvent = new WorkOrderEvent
        {
            WorkOrderId = order.Id, FromStatus = from, ToStatus = request.Status,
            ChangedBy = userName, ChangedAt = DateTimeOffset.UtcNow, Notes = request.Notes,
            DataClassification = DataClassification.Observed
        };
        _db.WorkOrderEvents.Add(transitionEvent);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetWorkOrderAsync(order.Id, cancellationToken))!;
    }

    public async Task<WorkOrderStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var total = await _db.WorkOrders.CountAsync(cancellationToken);
        var open = await _db.WorkOrders.CountAsync(w => w.Status != WorkOrderStatus.Closed && w.Status != WorkOrderStatus.Cancelled, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var overdue = await _db.WorkOrders.CountAsync(w => w.DueAt != null && w.DueAt < now &&
            w.Status != WorkOrderStatus.Closed && w.Status != WorkOrderStatus.Cancelled &&
            w.Status != WorkOrderStatus.Verified && w.Status != WorkOrderStatus.Resolved, cancellationToken);
        var byStatus = await _db.WorkOrders.GroupBy(w => w.Status)
            .Select(g => new CountByLabel(g.Key.ToString(), g.Count(), null)).ToListAsync(cancellationToken);
        var byPriority = await _db.WorkOrders.GroupBy(w => w.Priority)
            .Select(g => new CountByLabel(g.Key.ToString(), g.Count(), null)).ToListAsync(cancellationToken);
        var byType = await _db.WorkOrders.GroupBy(w => w.Type)
            .Select(g => new CountByLabel(g.Key.ToString(), g.Count(), null)).ToListAsync(cancellationToken);
        var message = total == 0
            ? "No work orders have been created. Field operations are idle."
            : "Transitions are enforced server-side. Verification requires a different user from the resolver.";
        return new WorkOrderStatsDto(total, open, overdue, byStatus, byPriority, byType, message);
    }

    private static WorkOrderDto ToDto(WorkOrder w) => new(
        w.Id, w.Reference, w.Title, w.Description, w.Type, w.Priority, w.Status,
        w.AssetId, w.Asset?.Name, w.Geometry?.X, w.Geometry?.Y, w.ReportedBy, w.AssignedTo, w.DueAt,
        w.CreatedAt, w.ResolvedAt, w.VerifiedAt, w.ClosedAt, w.ResolutionNotes, w.VerificationNotes,
        w.ResolvedBy, w.VerifiedBy, w.ClosedBy, w.DataClassification,
        w.DueAt is not null && w.DueAt < DateTimeOffset.UtcNow &&
        w.Status is not (WorkOrderStatus.Resolved or WorkOrderStatus.Verified or WorkOrderStatus.Closed or WorkOrderStatus.Cancelled));
}
