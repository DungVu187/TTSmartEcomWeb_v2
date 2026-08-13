namespace TTSmartEcom.Api.Contracts.Audit.Requests;

public sealed record ActivityQueryRequest(int? Page, int? Limit, DateTimeOffset? StartDate, DateTimeOffset? EndDate, string? UserName, string? ProductName, string? Action);
