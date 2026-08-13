namespace TTSmartEcom.Api.Contracts.Users.Responses;

public sealed record AuthenticatedUserResponse(
    string Id,
    string? Email,
    string Phone,
    string? Name,
    string Role,
    IReadOnlyCollection<string> Functions,
    IReadOnlyCollection<string> Permissions);
