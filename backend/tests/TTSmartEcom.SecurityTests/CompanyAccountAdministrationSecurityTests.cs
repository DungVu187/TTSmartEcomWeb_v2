using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;
using TtsApplicationException = TTSmartEcom.Application.Common.Errors.ApplicationException;

namespace TTSmartEcom.SecurityTests;

public sealed class CompanyAccountAdministrationSecurityTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();

    [Fact]
    public async Task SEC_COMPANY_ACCOUNT_001_CrossCompanyMutationIsDenied()
    {
        FakeRepository repository = new();
        CompanyAccountAdministrationService service = new(repository, new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.UpsertMembershipAsync(
                Guid.NewGuid(), TargetId.ToString(), 3, RoleId, Manager(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
        Assert.False(repository.WasCalled);
    }

    [Fact]
    public async Task SEC_COMPANY_ACCOUNT_002_BranchAccountManageDoesNotAuthorizeCompanyMutation()
    {
        FakeRepository repository = new();
        CompanyAccountAdministrationService service = new(repository, new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.UpsertMembershipAsync(
                CompanyId, TargetId.ToString(), 3, RoleId, Manager(branchOnlyPermission: true), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
        Assert.False(repository.WasCalled);
    }

    [Theory]
    [InlineData(CompanyMembershipMutationStatus.RoleHasWrongScope, 403)]
    [InlineData(CompanyMembershipMutationStatus.RoleBelongsToAnotherCompany, 403)]
    [InlineData(CompanyMembershipMutationStatus.RoleExceedsActorPermissions, 403)]
    [InlineData(CompanyMembershipMutationStatus.TargetIsPlatformIdentity, 403)]
    [InlineData(CompanyMembershipMutationStatus.LastOwner, 409)]
    public async Task SEC_COMPANY_ACCOUNT_003_PersistenceSecurityDecisionIsEnforced(
        CompanyMembershipMutationStatus status,
        int expectedStatus)
    {
        CompanyAccountAdministrationService service = new(new FakeRepository(status), new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.UpsertMembershipAsync(
                CompanyId, TargetId.ToString(), 3, RoleId, Manager(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(expectedStatus, error.Error.HttpStatus);
    }

    private static CurrentUserContext Manager(bool branchOnlyPermission = false)
    {
        IReadOnlySet<string> companyPermissions = branchOnlyPermission
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(["account.manage"], StringComparer.Ordinal);
        IReadOnlySet<string> branchPermissions = branchOnlyPermission
            ? new HashSet<string>(["account.manage"], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        CompanyMembershipContext company = new(
            CompanyId, "TTS", "TTSmart", Guid.NewGuid(), 2, ["ADMIN"], companyPermissions);
        BranchMembershipContext branch = new(
            CompanyId, BranchId, "MAIN", "Chi nhánh chính", Guid.NewGuid(), true, ["BRANCH_ADMIN"], branchPermissions);
        return new CurrentUserContext(
            Guid.NewGuid(), true, false, "Admin", "admin@example.test", null,
            [company], CompanyId, [branch], branchOnlyPermission ? BranchId : null,
            ["ADMIN"], new HashSet<string>(companyPermissions.Concat(branchPermissions), StringComparer.Ordinal), true);
    }

    private sealed class FakeRepository(
        CompanyMembershipMutationStatus status = CompanyMembershipMutationStatus.Success)
        : ICompanyAccountAdministrationRepository
    {
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<CompanyAccountMembership>> ListMembershipsAsync(
            Guid companyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyAccountMembership>>([]);

        public Task<IReadOnlyList<CompanyRoleDefinition>> ListCompanyRolesAsync(
            Guid companyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyRoleDefinition>>([]);

        public Task<CompanyMembershipMutationResult> UpsertMembershipAsync(
            CompanyMembershipUpsertCommand command,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            CompanyAccountMembership? membership = status == CompanyMembershipMutationStatus.Success
                ? new CompanyAccountMembership(
                    Guid.NewGuid(), CompanyId, TargetId, "Target", null, null,
                    ControlPlaneAccountType.Company, ControlPlaneUserType.Member, 1, [])
                : null;
            return Task.FromResult(new CompanyMembershipMutationResult(status, status == CompanyMembershipMutationStatus.Success, membership));
        }

        public Task<CompanyMembershipMutationResult> RevokeMembershipAsync(
            CompanyMembershipRevokeCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CompanyMembershipMutationResult(status, status == CompanyMembershipMutationStatus.Success));
    }
}
