using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Security;

public sealed class PermissionAuthorizationHandler(IOptions<LegacyCompatibilityOptions> options)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        UserIdentitySnapshot? identity = context.Resource switch
        {
            HttpContext httpContext => httpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot,
            _ => null,
        };

        if (identity is null)
        {
            return Task.CompletedTask;
        }

        bool allowed = identity.Role == SystemRoles.SuperAdmin
            || (identity.Role == SystemRoles.Admin && options.Value.AdminFullAccess)
            || identity.Permissions.Contains(requirement.Permission, StringComparer.Ordinal);

        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
