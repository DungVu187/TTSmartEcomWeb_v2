using System.Text.Json.Serialization;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Contracts.Users;

public sealed record CompanyMembershipUpsertRequest(
    [property: JsonPropertyName("userType")] byte UserType,
    [property: JsonPropertyName("roleId")] Guid RoleId);

public sealed record CompanyRoleResponse(
    [property: JsonPropertyName("roleId")] Guid RoleId,
    [property: JsonPropertyName("companyId")] Guid? CompanyId,
    [property: JsonPropertyName("roleCode")] string RoleCode,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("scopeType")] byte ScopeType,
    [property: JsonPropertyName("isSystemTemplate")] bool IsSystemTemplate,
    [property: JsonPropertyName("permissions")] IReadOnlyCollection<string> Permissions)
{
    public static CompanyRoleResponse From(CompanyRoleDefinition role) => new(
        role.RoleId,
        role.CompanyId,
        role.RoleCode,
        role.Name,
        (byte)role.ScopeType,
        role.IsSystemTemplate,
        role.Permissions.Order(StringComparer.Ordinal).ToArray());
}

public sealed record CompanyAccountResponse(
    [property: JsonPropertyName("companyUserId")] Guid CompanyUserId,
    [property: JsonPropertyName("companyId")] Guid CompanyId,
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("accountType")] byte AccountType,
    [property: JsonPropertyName("userType")] byte UserType,
    [property: JsonPropertyName("status")] byte Status,
    [property: JsonPropertyName("roles")] IReadOnlyCollection<CompanyRoleResponse> Roles)
{
    public static CompanyAccountResponse From(CompanyAccountMembership membership) => new(
        membership.CompanyUserId,
        membership.CompanyId,
        membership.UserId,
        membership.DisplayName,
        membership.Email,
        membership.Phone,
        (byte)membership.AccountType,
        (byte)membership.UserType,
        membership.Status,
        membership.Roles.Select(CompanyRoleResponse.From).ToArray());
}
