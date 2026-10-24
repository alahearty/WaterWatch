using System.ComponentModel.DataAnnotations;
using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record UserDto(
    string Id, string Email, string DisplayName, string? Organisation, bool IsActive,
    DateTimeOffset CreatedAt, IReadOnlyList<string> Roles);

public sealed class UserRoleUpdateRequest
{
    [Required] public List<string> Roles { get; set; } = new();
}

public sealed record AuditEntryDto(
    Guid Id, DateTimeOffset OccurredAt, string? UserName, string Action, string EntityType,
    string? EntityId, string? DetailsJson, string? IpAddress);

public sealed record ImportBatchDto(
    Guid Id, string FileName, string? TargetLayerKey, string? Format, string? Status,
    int RowsTotal, int RowsAccepted, int RowsRejected, DateTimeOffset CreatedAt);

public sealed record NotificationDto(string Type, string Title, string? Message, string? Url, DateTimeOffset OccurredAt, string Severity);
