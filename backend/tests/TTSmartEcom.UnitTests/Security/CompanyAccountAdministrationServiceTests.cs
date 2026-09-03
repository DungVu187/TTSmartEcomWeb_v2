using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;
using TtsApplicationException = TTSmartEcom.Application.Common.Errors.ApplicationException;

namespace TTSmartEcom.UnitTests.Security;

public sealed class CompanyAccountAdministrationServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();

    [Fact]
    public async Task UpsertMembership_AssignsCompanyMembershipAndRoleWithTrustedActorContext()
    {
        FakeRepository repository = new()
        {
            UpsertResult = new(CompanyMembershipMutationStatus.Success, true, Membership()),
        };
        CompanyAccountAdministrationService service = new(repository, new AccessScopeService());

        CompanyAccountMembership result = await service.UpsertMembershipAsync(
            CompanyId,
            UserId.ToString(),
            (byte)ControlPlaneUserType.Member,
            RoleId,
            CompanyManager(),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal(UserId, result.UserId);
        CompanyMembershipUpsertCommand command = Assert.IsType<CompanyMembershipUpsertCommand>(repository.LastUpsert);
        Assert.Equal(CompanyId, command.CompanyId);
        Assert.Equal(UserId, command.TargetUserId);
        Assert.Equal(RoleId, command.RoleId);
        Assert.Equal(ControlPlaneUserType.Member, command.UserType);
        Assert.Contains("account.manage", command.ActorCompanyPermissions);
    }

    [Fact]
    public async Task UpsertMembership_RejectsCrossCompanyBeforeRepositoryCall()
    {
        FakeRepository repository = new();
        CompanyAccountAdministrationService service = new(repository, new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.UpsertMembershipAsync(
                Guid.NewGuid(), UserId.ToString(), 3, RoleId, CompanyManager(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
        Assert.Null(repository.LastUpsert);
    }

    [Fact]
    public async Task ListMemberships_RejectsMissingCompanyAccountManagePermission()
    {
        FakeRepository repository = new();
        CompanyAccountAdministrationService service = new(repository, new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.ListMembershipsAsync(
                CompanyId,
                CompanyManager(new HashSet<string>(StringComparer.Ordinal)),
                CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
        Assert.Contains("account.manage", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CompanyMembershipMutationStatus.RoleHasWrongScope, 403)]
    [InlineData(CompanyMembershipMutationStatus.TargetIsPlatformIdentity, 403)]
    [InlineData(CompanyMembershipMutationStatus.RoleExceedsActorPermissions, 403)]
    [InlineData(CompanyMembershipMutationStatus.MembershipTypeExceedsActor, 403)]
    [InlineData(CompanyMembershipMutationStatus.ControlPlaneIdentityNotFound, 404)]
    [InlineData(CompanyMembershipMutationStatus.RoleNotFound, 404)]
    [InlineData(CompanyMembershipMutationStatus.LastOwner, 409)]
    [InlineData(CompanyMembershipMutationStatus.Conflict, 409)]
    public async Task UpsertMembership_MapsRepositoryBoundaryStatus(
        CompanyMembershipMutationStatus status,
        int expectedHttpStatus)
    {
        FakeRepository repository = new() { UpsertResult = new(status, false) };
        CompanyAccountAdministrationService service = new(repository, new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.UpsertMembershipAsync(
                CompanyId, UserId.ToString(), 3, RoleId, CompanyManager(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(expectedHttpStatus, error.Error.HttpStatus);
    }

    [Fact]
    public async Task UpsertMembership_ReturnsClearConflictForLegacyOperationalIdentifier()
    {
        CompanyAccountAdministrationService service = new(new FakeRepository(), new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.UpsertMembershipAsync(
                CompanyId,
                "507f191e810c19729de860ea",
                3,
                RoleId,
                CompanyManager(),
                Guid.NewGuid(),
                CancellationToken.None));

        Assert.Equal(409, error.Error.HttpStatus);
        Assert.Contains("legacy Operational", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokeMembership_RejectsLastOwnerConflict()
    {
        FakeRepository repository = new()
        {
            RevokeResult = new(CompanyMembershipMutationStatus.LastOwner, false),
        };
        CompanyAccountAdministrationService service = new(repository, new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.RevokeMembershipAsync(
                CompanyId, UserId.ToString(), CompanyManager(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(409, error.Error.HttpStatus);
    }

    [Fact]
    public async Task ListMemberships_RequiresAuthentication()
    {
        CompanyAccountAdministrationService service = new(new FakeRepository(), new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.ListMembershipsAsync(CompanyId, CurrentUserContext.Anonymous, CancellationToken.None));

        Assert.Equal(401, error.Error.HttpStatus);
    }

    private static CurrentUserContext CompanyManager(IReadOnlySet<string>? permissions = null)
    {
        IReadOnlySet<string> granted = permissions ?? new HashSet<string>(
            ["account.manage", "product.edit"], StringComparer.Ordinal);
        CompanyMembershipContext membership = new(
            CompanyId,
            "TTSMART",
            "TTSmart",
            Guid.NewGuid(),
            (byte)ControlPlaneUserType.Admin,
            ["company_admin"],
            granted);
        return new CurrentUserContext(
            Guid.NewGuid(), true, false, "Company Admin", "admin@example.test", null,
            [membership], CompanyId, [], null, ["company_admin"], granted, true);
    }

    private static CompanyAccountMembership Membership() => new(
        Guid.NewGuid(),
        CompanyId,
        UserId,
        "User",
        "user@example.test",
        null,
        ControlPlaneAccountType.Company,
        ControlPlaneUserType.Member,
        1,
        [new CompanyRoleDefinition(
            RoleId, CompanyId, "MEMBER", "Thành viên", ControlPlaneScopeType.Company, false,
            new HashSet<string>(["product.view"], StringComparer.Ordinal))]);

    private sealed class FakeRepository : ICompanyAccountAdministrationRepository
    {
        public CompanyMembershipMutationResult UpsertResult { get; init; } =
            new(CompanyMembershipMutationStatus.Success, true, Membership());
        public CompanyMembershipMutationResult RevokeResult { get; init; } =
            new(CompanyMembershipMutationStatus.Success, true);
        public CompanyMembershipUpsertCommand? LastUpsert { get; private set; }

        public Task<IReadOnlyList<CompanyAccountMembership>> ListMembershipsAsync(
            Guid companyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyAccountMembership>>([Membership()]);

        public Task<IReadOnlyList<CompanyRoleDefinition>> ListCompanyRolesAsync(
            Guid companyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyRoleDefinition>>(Membership().Roles);

        public Task<CompanyMembershipMutationResult> UpsertMembershipAsync(
            CompanyMembershipUpsertCommand command,
            CancellationToken cancellationToken)
        {
            LastUpsert = command;
            return Task.FromResult(UpsertResult);
        }

        public Task<CompanyMembershipMutationResult> RevokeMembershipAsync(
            CompanyMembershipRevokeCommand command,
            CancellationToken cancellationToken) => Task.FromResult(RevokeResult);
    }
}
