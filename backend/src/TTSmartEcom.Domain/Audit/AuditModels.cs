using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Audit;

public sealed record ActivityLog(
    [property: JsonPropertyName("_id")] string Id,
    string? UserName,
    string? Action,
    [property: JsonPropertyName("productId")] string? ProductId,
    string? ProductName,
    IReadOnlyList<ActivityLogDetail> Details,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ActivityLogDetail(string? Field, string? OldValue, string? NewValue);

public sealed record ActivityLogQuery(int Page, int Limit, DateTimeOffset? StartDate, DateTimeOffset? EndDate,
    string? UserName, string? ProductName, string? Action);

public sealed record ActivityLogPage(bool Success, int Page, int Limit, long Total, int TotalPages,
    IReadOnlyList<ActivityLog> Logs,
    IReadOnlyDictionary<string, string> ActionLabels,
    ActivityLogReferences? References = null);

public sealed record ActivityLogReferences(
    IReadOnlyDictionary<string, string> Products,
    IReadOnlyDictionary<string, string> Stations);
