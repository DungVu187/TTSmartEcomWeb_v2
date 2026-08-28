using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.UnitTests.Security;

public sealed class PermissionAuthorizationHandlerScopeTests
{
    [Fact]
    public async Task ControlPlanePermission_IsDeniedWhenOnlyAnotherCompanyGrantsIt()
    {
        Guid companyA = Guid.NewGuid();
        Guid companyB = Guid.NewGuid();
        CurrentUserContext user = CreateMultiCompanyUser(companyA, companyB, companyB);

        AuthorizationHandlerContext authorization = await AuthorizeAsync(user, "product.edit");

        Assert.False(authorization.HasSucceeded);
    }

    [Fact]
    public async Task ControlPlanePermission_IsAllowedWhenActiveCompanyGrantsIt()
    {
        Guid companyA = Guid.NewGuid();
        Guid companyB = Guid.NewGuid();
        CurrentUserContext user = CreateMultiCompanyUser(companyA, companyB, companyA);

        AuthorizationHandlerContext authorization = await AuthorizeAsync(user, "product.edit");

        Assert.True(authorization.HasSucceeded);
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(CurrentUserContext user, string permission)
    {
        DefaultHttpContext http = new();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.UserId!.Value.ToString())], "test"));
        http.Items[CurrentUserContextMiddleware.ContextItemKey] = user;
        AuthorizationHandlerContext context = new([new PermissionRequirement(permission)], http.User, http);
        PermissionAuthorizationHandler handler = new(
            Options.Create(new LegacyCompatibilityOptions { AdminFullAccess = true }), new AccessScopeService());

        await handler.HandleAsync(context);
        return context;
    }

    private static CurrentUserContext CreateMultiCompanyUser(Guid companyA, Guid companyB, Guid activeCompany) => new(
        Guid.NewGuid(), true, false, "User", null, null,
        [
            new CompanyMembershipContext(companyA, "ABC", "ABC", Guid.NewGuid(), (byte)ControlPlaneUserType.Admin,
                ["company_admin"], new HashSet<string>(["product.edit"], StringComparer.Ordinal)),
            new CompanyMembershipContext(companyB, "XYZ", "XYZ", Guid.NewGuid(), (byte)ControlPlaneUserType.Member,
                ["company_member"], new HashSet<string>(StringComparer.Ordinal)),
        ],
        activeCompany, [], null, ["company_admin", "company_member"],
        new HashSet<string>(["product.edit"], StringComparer.Ordinal), isControlPlaneIdentity: true);
}
