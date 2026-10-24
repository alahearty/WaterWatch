using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Dtos;
using WaterWatch.Security;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.CanAdminister)]
public class AdminApiController : ControllerBase
{
    private readonly DatabaseContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public AdminApiController(DatabaseContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        _db = db;
        _users = users;
        _roles = roles;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Users(CancellationToken cancellationToken)
    {
        var users = await _users.Users.OrderBy(u => u.Email).ToListAsync(cancellationToken);
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _users.GetRolesAsync(user);
            result.Add(new UserDto(user.Id, user.Email ?? string.Empty, user.DisplayName,
                user.Organisation, user.IsActive, user.CreatedAt, roles.ToList()));
        }
        return Ok(result);
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<string>>> RoleList(CancellationToken cancellationToken) =>
        Ok(await _roles.Roles.Select(r => r.Name ?? string.Empty).OrderBy(r => r).ToListAsync(cancellationToken));

    [HttpPut("users/{id}/roles")]
    public async Task<ActionResult<UserDto>> UpdateRoles(string id, [FromBody] UserRoleUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }
        var available = await _roles.Roles.Select(r => r.Name!).ToListAsync(cancellationToken);
        var requested = request.Roles.Where(r => available.Contains(r)).Distinct().ToList();
        if (requested.Count == 0)
        {
            return BadRequest(new { error = "At least one valid role is required." });
        }
        if (await _users.IsInRoleAsync(user, Roles.Administrator) && !requested.Contains(Roles.Administrator))
        {
            var adminCount = (await _users.GetUsersInRoleAsync(Roles.Administrator))
                .Count(u => u.IsActive);
            if (adminCount <= 1)
            {
                return BadRequest(new { error = "The last active administrator cannot lose the Administrator role." });
            }
        }
        var current = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, current.Except(requested));
        await _users.AddToRolesAsync(user, requested.Except(current));
        return Ok(new UserDto(user.Id, user.Email ?? string.Empty, user.DisplayName, user.Organisation,
            user.IsActive, user.CreatedAt, (await _users.GetRolesAsync(user)).ToList()));
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> Audit([FromQuery] int limit = 100, CancellationToken cancellationToken = default) =>
        Ok(await _db.AuditEntries.AsNoTracking().OrderByDescending(a => a.OccurredAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(a => new AuditEntryDto(a.Id, a.OccurredAt, a.UserName, a.Action, a.EntityType,
                a.EntityId, a.DetailsJson, a.IpAddress))
            .ToListAsync(cancellationToken));

    [HttpGet("imports")]
    public async Task<ActionResult<IReadOnlyList<ImportBatchDto>>> Imports([FromQuery] int limit = 50, CancellationToken cancellationToken = default) =>
        Ok(await _db.ImportBatches.AsNoTracking().OrderByDescending(b => b.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(b => new ImportBatchDto(b.Id, b.FileName, b.TargetLayerKey, b.Format,
                b.Status.ToString(), b.RowsTotal, b.RowsAccepted, b.RowsRejected, b.CreatedAt))
            .ToListAsync(cancellationToken));
}
