namespace TTSmartEcom.Api.Contracts.Users.Requests;

public sealed record LoginRequest(
    string? Identifier,
    string? Password,
    string? Phone = null,
    string? Email = null,
    string? InviteCode = null)
{
    public string? ResolveIdentifier() => FirstNonEmpty(Identifier, Phone, Email);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
