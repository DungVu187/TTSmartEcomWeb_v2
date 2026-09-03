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
    public async Task AssignAsync_RepeatedAssignmentIsIdempotent()
    {
        FakeAssignments assignments = new() { ChangedCount = 0 };
        FakeBranches branches = new(new BranchCompanyReference(BranchId, CompanyId, "HN", true));
        ProductBranchDistributionService service = new(assignments, branches, new AccessScopeService());

        ProductBranchAssignmentChange result = await service.AssignAsync(
            [ProductId, ProductId], [BranchId, BranchId], CompanyAdmin(), CancellationToken.None);

        Assert.Equal(0, result.ChangedCount);
        Assert.Single(result.ProductIds);
        Assert.Single(result.BranchIds);
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

    private static CurrentUserContext CompanyAdmin(IReadOnlySet<string>? permissions = null)
    {
        IReadOnlySet<string> granted = permissions ?? new HashSet<string>(["product.edit"], StringComparer.Ordinal);
        CompanyMembershipContext membership = new(
            CompanyId, "TTSmart", "TTSmart", Guid.NewGuid(), 1, ["company_admin"], granted);
        return new CurrentUserContext(
            Guid.NewGuid(), true, false, "Company Admin", "admin@example.test", null,
            [membership], CompanyId, [], null, ["company_admin"], granted, true);
    }

    private sealed class FakeBranches(params BranchCompanyReference[] values) : ICompanyBranchDirectory
    {
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
