namespace TTSmartEcom.Application.Abstractions.Authentication;

public sealed record UserIdentitySnapshot(
    string Id,
    string? Email,
    string Phone,
    string? Name,
    string Role,
    IReadOnlyCollection<string> Functions,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset? PasswordChangedAt,
    IReadOnlyCollection<string>? StationIds = null);
