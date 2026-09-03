using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Application.Abstractions.Products;

public sealed record BranchCompanyReference(
    Guid BranchId,
    Guid CompanyId,
    string BranchCode,
    bool IsActive);

public sealed record ProductBranchAssignmentQueryResult(
    bool ProductExists,
    IReadOnlyList<ProductBranchAssignment> Assignments);

public sealed record ProductBranchAssignmentMutationResult(
    bool ProductsExist,
    long ChangedCount,
    IReadOnlyList<ProductBranchAssignment> Assignments,
    IReadOnlyList<string> MissingProductIds);

public interface ICompanyBranchDirectory
{
    Task<IReadOnlyDictionary<Guid, BranchCompanyReference>> FindBranchesAsync(
        IReadOnlyCollection<Guid> branchIds,
        CancellationToken cancellationToken);
}

public interface IProductBranchAssignmentRepository
{
    Task<ProductBranchAssignmentQueryResult> ListForProductAsync(
        Guid companyId,
        string productPublicId,
        CancellationToken cancellationToken);

    Task<bool?> IsActiveAsync(
        Guid companyId,
        string productPublicId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<ProductBranchAssignmentMutationResult> SetActiveAsync(
        Guid companyId,
        IReadOnlyCollection<string> productPublicIds,
        IReadOnlyCollection<Guid> branchIds,
        bool isActive,
        Guid? actorUserId,
        string actorName,
        CancellationToken cancellationToken);
}
