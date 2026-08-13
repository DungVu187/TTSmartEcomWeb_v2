namespace TTSmartEcom.Api.Contracts.Users.Requests;

public sealed record UpdateProfileRequest(string? Name, string? Email);
public sealed record AddressRequest(string? Label, string? ReceiverName, string? ReceiverPhone, string? AddressDetail);
public sealed record TemplateRequest(string? DisplayName, IReadOnlyList<TemplateProductRequest>? Products);
public sealed record TemplateProductRequest(string? ProductId, double? Quantity);
public sealed record CreateAdminUserRequest(string? Email, string? Phone, string? Name, string? Password, string? Role, IReadOnlyList<string>? Permissions);
public sealed record UpdateUserRequest(string? Name, string? Email, string? Phone);
public sealed record UpdatePermissionsRequest(string? Role, IReadOnlyList<string>? Permissions, string? Name, string? Email, string? Phone, string? Password);
public sealed record ReplaceStationsRequest(string? Phone, IReadOnlyList<string>? Stations);
public sealed record AddStationRequest(string? StationId);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record AutoLoginRequest(string? Token);
public sealed record ForgotPasswordRequest(string? Identifier, string? Phone, string? Email)
{
    public string? ResolveIdentifier() =>
        new[] { Identifier, Phone, Email }
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            ?.Trim();
}
public sealed record ResetPasswordRequest(string? Identifier, string? Phone, string? Email, string? Otp, string? NewPassword)
{
    public string? ResolveIdentifier() =>
        new[] { Identifier, Phone, Email }
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            ?.Trim();
}
public sealed record RegisterUserRequest(
    string? Email,
    string? Phone,
    string? Name,
    string? Password,
    string? Role,
    IReadOnlyList<string>? Permissions,
    string? LogInString,
    string? StationCode,
    string? InviteCode);
