using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Security;
using TtsApplicationException = TTSmartEcom.Application.Common.Errors.ApplicationException;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductBranchDistributionServiceTests
{
    private const string ProductId = "507f191e810c19729de860ea";
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    [Fact]
    public async Task AssignAsync_RejectsBranchOwnedByAnotherCompany()
    {
        FakeAssignments assignments = new();
        FakeBranches branches = new(new BranchCompanyReference(BranchId, Guid.NewGuid(), "SG", true));
        ProductBranchDistributionService service = new(assignments, branches, new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.AssignAsync([ProductId], [BranchId], CompanyAdmin(), CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
        Assert.Equal(0, assignments.CallCount);
    }

    [Fact]
    public async Task AssignAsync_RepeatedAssignmentDoesNotReportFalseSuccess()
    {
        FakeAssignments assignments = new() { ChangedCount = 0 };
        FakeBranches branches = new(new BranchCompanyReference(BranchId, CompanyId, "HN", true));
        ProductBranchDistributionService service = new(assignments, branches, new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.AssignAsync([ProductId, ProductId], [BranchId, BranchId], CompanyAdmin(), CancellationToken.None));

        Assert.Equal(409, error.Error.HttpStatus);
        Assert.Equal(1, assignments.CallCount);
    }

    [Fact]
    public async Task RevokeAsync_DoesNotDeleteAssignment()
    {
        FakeAssignments assignments = new() { ChangedCount = 1 };
        FakeBranches branches = new(new BranchCompanyReference(BranchId, CompanyId, "HN", true));
        ProductBranchDistributionService service = new(assignments, branches, new AccessScopeService());

        ProductBranchAssignmentChange result = await service.RevokeAsync(
            [ProductId], [BranchId], CompanyAdmin(), CancellationToken.None);

        Assert.Equal(1, result.ChangedCount);
        Assert.False(assignments.LastIsActive);
    }

    [Fact]
    public async Task DistributionStatus_LoadsAllActiveBranchesInOneRepositoryCall()
    {
        FakeAssignments assignments = new();
        FakeBranches branches = new(new BranchCompanyReference(BranchId, CompanyId, "HN", true));
        ProductBranchDistributionService service = new(assignments, branches, new AccessScopeService());

        IReadOnlyList<ProductBranchDistributionStatus> result = await service.GetDistributionStatusAsync(
            [ProductId], CompanyAdmin(), CancellationToken.None);

        ProductBranchDistributionStatus status = Assert.Single(result);
        Assert.Equal(BranchId, status.BranchId);
        Assert.Equal(1, assignments.StatusCallCount);
    }

    [Fact]
    public async Task AssignAsync_RequiresCompanyPermissionFromActiveScope()
    {
        CurrentUserContext user = CompanyAdmin(permissions: new HashSet<string>(StringComparer.Ordinal));
        ProductBranchDistributionService service = new(
            new FakeAssignments(),
            new FakeBranches(new BranchCompanyReference(BranchId, CompanyId, "HN", true)),
            new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.AssignAsync([ProductId], [BranchId], user, CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
    }

    [Fact]
    public async Task AssignAsync_BranchOnlyProductEditCannotDistributeAtCompanyScope()
    {
        FakeAssignments assignments = new();
        ProductBranchDistributionService service = new(
            assignments,
            new FakeBranches(new BranchCompanyReference(BranchId, CompanyId, "HN", true)),
            new AccessScopeService());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.AssignAsync([ProductId], [BranchId], BranchEditor(companyProductEdit: false), CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
        Assert.Equal(0, assignments.CallCount);
    }

    [Fact]
    public async Task AssignAsync_CompanyProductEditCanDistributeWhileBranchIsActive()
    {
        FakeAssignments assignments = new() { ChangedCount = 1 };
        ProductBranchDistributionService service = new(
            assignments,
            new FakeBranches(new BranchCompanyReference(BranchId, CompanyId, "HN", true)),
            new AccessScopeService());

        ProductBranchAssignmentChange result = await service.AssignAsync(
            [ProductId], [BranchId], BranchEditor(companyProductEdit: true), CancellationToken.None);

        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(1, assignments.CallCount);
    }

    [Fact]
    public async Task ResolveCreationAssignmentAsync_UsesTrustedActiveBranch()
    {
        FakeBranches branches = new(new BranchCompanyReference(BranchId, CompanyId, "HN", true));
        ProductBranchDistributionService service = new(new FakeAssignments(), branches, new AccessScopeService());
        CurrentUserContext context = BranchProductCreator();

        ProductCreationAssignment assignment = await service.ResolveCreationAssignmentAsync(
            context,
            CancellationToken.None);

        Assert.Equal(CompanyId, assignment.CompanyId);
        Assert.Equal(BranchId, assignment.BranchId);
        Assert.Equal(context.UserId, assignment.ActorUserId);
    }

    [Fact]
    public async Task ResolveCreationAssignmentAsync_InCompanyWorkspace_DoesNotInventBranch()
    {
        ProductBranchDistributionService service = new(
            new FakeAssignments(),
            new FakeBranches(),
            new AccessScopeService());
        CurrentUserContext context = CompanyAdmin(
            new HashSet<string>(["product.create"], StringComparer.Ordinal));

        ProductCreationAssignment assignment = await service.ResolveCreationAssignmentAsync(
            context,
            CancellationToken.None);

        Assert.Equal(CompanyId, assignment.CompanyId);
        Assert.Null(assignment.BranchId);
    }

    private static CurrentUserContext CompanyAdmin(IReadOnlySet<string>? permissions = null)
    {
        IReadOnlySet<string> granted = permissions ?? new HashSet<string>(["product.edit"], StringComparer.Ordinal);
        CompanyMembershipContext membership = new(
            CompanyId, "TTSmart", "TTSmart", Guid.NewGuid(), 1, ["company_admin"], granted);
        return new CurrentUserContext(
            Guid.NewGuid(), true, false, "Company Admin", "admin@example.test", null,
            [membership], CompanyId, [], null, ["company_admin"], granted, true);
    }

    private static CurrentUserContext BranchProductCreator()
    {
        IReadOnlySet<string> permissions = new HashSet<string>(["product.create"], StringComparer.Ordinal);
        CompanyMembershipContext company = new(
            CompanyId, "TTSmart", "TTSmart", Guid.NewGuid(), 1, ["company_admin"], permissions);
        BranchMembershipContext branch = new(
            CompanyId, BranchId, "HN", "Hà Nội", Guid.NewGuid(), true, ["company_admin"], permissions);
        return new CurrentUserContext(
            Guid.NewGuid(), true, false, "Company Admin", "admin@example.test", null,
            [company], CompanyId, [branch], BranchId, ["company_admin"], permissions, true);
    }

    private static CurrentUserContext BranchEditor(bool companyProductEdit)
    {
        IReadOnlySet<string> companyPermissions = companyProductEdit
            ? new HashSet<string>(["product.edit"], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        IReadOnlySet<string> branchPermissions = new HashSet<string>(["product.edit"], StringComparer.Ordinal);
        CompanyMembershipContext company = new(
            CompanyId, "TTSmart", "TTSmart", Guid.NewGuid(), 3, ["member"], companyPermissions);
        BranchMembershipContext branch = new(
            CompanyId, BranchId, "HN", "Hà Nội", Guid.NewGuid(), true, ["branch_editor"], branchPermissions);
        return new CurrentUserContext(
            Guid.NewGuid(), true, false, "Branch Editor", "branch@example.test", null,
            [company], CompanyId, [branch], BranchId, ["member", "branch_editor"],
            new HashSet<string>(companyPermissions.Concat(branchPermissions), StringComparer.Ordinal), true);
    }

    private sealed class FakeBranches(params BranchCompanyReference[] values) : ICompanyBranchDirectory
    {
        public Task<IReadOnlyList<ActiveCompanyBranch>> ListActiveBranchesAsync(
            Guid companyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActiveCompanyBranch>>(values
                .Where(value => value.CompanyId == companyId && value.IsActive)
                .Select(value => new ActiveCompanyBranch(value.BranchId, value.CompanyId, value.BranchCode, value.BranchCode))
                .ToArray());

        public Task<IReadOnlyDictionary<Guid, BranchCompanyReference>> FindBranchesAsync(
            IReadOnlyCollection<Guid> branchIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, BranchCompanyReference>>(
                values.Where(value => branchIds.Contains(value.BranchId)).ToDictionary(value => value.BranchId));
    }

    private sealed class FakeAssignments : IProductBranchAssignmentRepository
    {
        public int CallCount { get; private set; }
        public long ChangedCount { get; init; }
        public bool LastIsActive { get; private set; }
        public int StatusCallCount { get; private set; }

        public Task<IReadOnlyList<ProductBranchDistributionStatus>> GetDistributionStatusAsync(
            Guid companyId, IReadOnlyCollection<string> productPublicIds,
            IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken)
        {
            StatusCallCount++;
            return Task.FromResult<IReadOnlyList<ProductBranchDistributionStatus>>(
                branchIds.Select(id => new ProductBranchDistributionStatus(id, 1, productPublicIds.Count)).ToArray());
        }

        public Task<ProductBranchAssignmentQueryResult> ListForProductAsync(Guid companyId, string productPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(new ProductBranchAssignmentQueryResult(true, []));

        public Task<bool?> IsActiveAsync(Guid companyId, string productPublicId, Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult<bool?>(true);

        public Task<ProductBranchAssignmentMutationResult> SetActiveAsync(
            Guid companyId,
            IReadOnlyCollection<string> productPublicIds,
            IReadOnlyCollection<Guid> branchIds,
            bool isActive,
            Guid? actorUserId,
            string actorName,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastIsActive = isActive;
            return Task.FromResult(new ProductBranchAssignmentMutationResult(true, ChangedCount, [], []));
        }
    }
}
