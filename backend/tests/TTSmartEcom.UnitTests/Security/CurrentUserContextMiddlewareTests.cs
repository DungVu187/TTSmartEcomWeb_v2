using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.UnitTests.Security;

public sealed class CurrentUserContextMiddlewareTests
{
    [Fact]
    public async Task ConflictingCompanyAndBranchHeaders_AreRejectedWith403()
    {
        Guid userId = Guid.NewGuid();
        Guid companyA = Guid.NewGuid();
        Guid companyB = Guid.NewGuid();
        Guid branchB = Guid.NewGuid();
        CurrentUserContext identity = new(
            userId, true, false, "User", null, null,
            [
                new CompanyMembershipContext(companyA, "ABC", "ABC", Guid.NewGuid(), 2, [], new HashSet<string>()),
                new CompanyMembershipContext(companyB, "XYZ", "XYZ", Guid.NewGuid(), 2, [], new HashSet<string>()),
            ],
            null,
            [new BranchMembershipContext(companyB, branchB, "HN", "Ha Noi", Guid.NewGuid(), true, [], new HashSet<string>())],
            null, [], new HashSet<string>(), isControlPlaneIdentity: true);

        DefaultHttpContext http = new();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", userId.ToString())], "test"));
        http.Request.Headers["X-Company-Id"] = companyA.ToString();
        http.Request.Headers["X-Branch-Id"] = branchB.ToString();

        bool nextCalled = false;
        CurrentUserContextMiddleware middleware = new(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new FixedControlPlaneIdentityReader(identity),
            new NullLegacyIdentityReader(),
            NullLogger<CurrentUserContextMiddleware>.Instance);

        await middleware.InvokeAsync(http);

        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task CompanyOnlySelection_ClearsPrimaryBranchFromAnotherCompany()
    {
        Guid userId = Guid.NewGuid();
        Guid companyA = Guid.NewGuid();
        Guid companyB = Guid.NewGuid();
        Guid branchA = Guid.NewGuid();
        CurrentUserContext identity = new(
            userId, true, false, "User", null, null,
            [
                new CompanyMembershipContext(companyA, "ABC", "ABC", Guid.NewGuid(), 2, [], new HashSet<string>()),
                new CompanyMembershipContext(companyB, "XYZ", "XYZ", Guid.NewGuid(), 2, [], new HashSet<string>()),
            ],
            null,
            [new BranchMembershipContext(companyA, branchA, "HN", "Ha Noi", Guid.NewGuid(), true, [], new HashSet<string>())],
            branchA, [], new HashSet<string>(), isControlPlaneIdentity: true);

        DefaultHttpContext http = new();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", userId.ToString())], "test"));
        http.Request.Headers["X-Company-Id"] = companyB.ToString();
        CurrentUserContextMiddleware middleware = new(
            _ => Task.CompletedTask,
            new FixedControlPlaneIdentityReader(identity),
            new NullLegacyIdentityReader(),
            NullLogger<CurrentUserContextMiddleware>.Instance);

        await middleware.InvokeAsync(http);

        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
        ICurrentUserContext resolved = Assert.IsAssignableFrom<ICurrentUserContext>(http.Items[CurrentUserContextMiddleware.ContextItemKey]);
        Assert.Equal(companyB, resolved.ActiveCompanyId);
        Assert.Null(resolved.ActiveBranchId);
    }

    [Fact]
    public async Task CompanyOnlySelection_ClearsPrimaryBranchFromSameCompany()
    {
        Guid userId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        CurrentUserContext identity = new(
            userId, true, false, "User", null, null,
            [new CompanyMembershipContext(companyId, "ABC", "ABC", Guid.NewGuid(), 2, [], new HashSet<string>())],
            companyId,
            [new BranchMembershipContext(companyId, branchId, "MAIN", "Main", Guid.NewGuid(), true, [], new HashSet<string>())],
            branchId, [], new HashSet<string>(), isControlPlaneIdentity: true);

        DefaultHttpContext http = new();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", userId.ToString())], "test"));
        http.Request.Headers["X-Company-Id"] = companyId.ToString();
        CurrentUserContextMiddleware middleware = new(
            _ => Task.CompletedTask,
            new FixedControlPlaneIdentityReader(identity),
            new NullLegacyIdentityReader(),
            NullLogger<CurrentUserContextMiddleware>.Instance);

        await middleware.InvokeAsync(http);

        ICurrentUserContext resolved = Assert.IsAssignableFrom<ICurrentUserContext>(http.Items[CurrentUserContextMiddleware.ContextItemKey]);
        Assert.Equal(companyId, resolved.ActiveCompanyId);
        Assert.Null(resolved.ActiveBranchId);
    }

    private sealed class FixedControlPlaneIdentityReader(ICurrentUserContext context) : IControlPlaneIdentityReader
    {
        public Task<ICurrentUserContext?> FindContextByIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<ICurrentUserContext?>(context);

        public Task<ICurrentUserContext?> FindContextByLoginAsync(string identifier, CancellationToken cancellationToken) => Task.FromResult<ICurrentUserContext?>(context);
    }

    private sealed class NullLegacyIdentityReader : IUserIdentityReader
    {
        public Task<UserIdentitySnapshot?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<UserIdentitySnapshot?>(null);
    }
}
