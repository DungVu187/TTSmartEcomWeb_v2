using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Audit;

public sealed record StorageHistoryEntry(
    [property: JsonPropertyName("_id")] string Id,
    string? ProductId,
    string? ProductName,
    double Quantity,
    string? UserName,
    string? OrderId,
    string? OrderName,
    string? Note,
    [property: JsonPropertyName("isAIScan")] bool IsAiScan,
    string? Source,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record StorageHistoryQuery(
    int Page,
    int Limit,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    string? OrderName,
    string? UserName,
    string? NoteType,
    string? Direction,
    bool ExportAll);

public sealed record StorageHistoryPage(
    bool Success,
    int Page,
    long Limit,
    long Total,
    int TotalPages,
    IReadOnlyList<StorageHistoryEntry> History);

public sealed record StorageHistoryFilterOptions(
    bool Success,
    IReadOnlyList<string> UserNames,
    IReadOnlyList<string> OrderNames);
