using WaterWatch.Domain.Enums;

namespace WaterWatch.Dtos;

public sealed record ReportRequest(
    string ReportType,
    ReportFormat Format,
    DateOnly? From,
    DateOnly? To,
    Guid? AdminAreaId,
    Guid? OperationalAreaId,
    string? Search,
    string? Dimension = null);

public sealed record ReportFileDto(byte[] Content, string ContentType, string FileName, int RecordCount, string ReportType);

public sealed record ReportJobDto(
    Guid Id, string Name, string ReportType, ReportFormat Format, string Status,
    int RecordCount, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? Error);

public sealed record ReportTypeDto(string Key, string Name, string Description, IReadOnlyList<string> SupportedFormats);
