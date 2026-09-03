namespace TTSmartEcom.Domain.Products;

public sealed record ProductBranchAssignment(
    string ProductId,
    Guid BranchId,
    bool IsActive,
    DateTimeOffset AssignedAtUtc,
    Guid? AssignedByUserId,
    DateTimeOffset? RevokedAtUtc,
    Guid? RevokedByUserId,
    string RowVersion);

public sealed record ProductBranchAssignmentChange(
    IReadOnlyList<string> ProductIds,
    IReadOnlyList<Guid> BranchIds,
    long ChangedCount);
