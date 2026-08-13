using Microsoft.AspNetCore.Authorization;

namespace TTSmartEcom.Api.Security;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
