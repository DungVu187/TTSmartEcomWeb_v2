using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Security;

public sealed class PermissionAuthorizationHandler(
    IOptions<LegacyCompatibilityOptions> options,
    IAccessScopeService accessScope)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        HttpContext? httpContext = context.Resource switch
        {
            HttpContext ctx => ctx,
            _ => null,
        };

        if (httpContext is null)
        {
            return Task.CompletedTask;
        }

        ICurrentUserContext? userContext = httpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;
        if (userContext is not null)
        {
            if (userContext.IsControlPlaneIdentity)
            {
                if (accessScope.HasActiveScopePermission(userContext, requirement.Permission))
                {
                    context.Succeed(requirement);
                }

                return Task.CompletedTask;
            }
        }

        UserIdentitySnapshot? identity = httpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        if (identity is not null)
        {
            bool allowed = identity.Role == SystemRoles.SuperAdmin
                || (identity.Role == SystemRoles.Admin && options.Value.AdminFullAccess)
                || identity.Permissions.Contains(requirement.Permission, StringComparer.Ordinal);

            if (allowed)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
