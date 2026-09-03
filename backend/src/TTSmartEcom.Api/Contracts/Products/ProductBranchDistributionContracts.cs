using System.Text.Json.Serialization;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Api.Contracts.Products;

public sealed record ProductBranchDistributionRequest(
    [property: JsonPropertyName("productIds")] IReadOnlyList<string>? ProductIds,
    [property: JsonPropertyName("branchIds")] IReadOnlyList<Guid>? BranchIds);

public sealed record ProductDistributionBranchResponse(
    [property: JsonPropertyName("branchId")] Guid BranchId,
    [property: JsonPropertyName("companyId")] Guid CompanyId,
    [property: JsonPropertyName("branchCode")] string BranchCode,
    [property: JsonPropertyName("name")] string Name);

public sealed record ProductBranchAssignmentResponse(
    [property: JsonPropertyName("productId")] string ProductId,
    [property: JsonPropertyName("branchId")] Guid BranchId,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("assignedAtUtc")] DateTimeOffset AssignedAtUtc,
    [property: JsonPropertyName("assignedByUserId")] Guid? AssignedByUserId,
    [property: JsonPropertyName("revokedAtUtc")] DateTimeOffset? RevokedAtUtc,
    [property: JsonPropertyName("revokedByUserId")] Guid? RevokedByUserId,
    [property: JsonPropertyName("rowVersion")] string RowVersion)
{
    public static ProductBranchAssignmentResponse From(ProductBranchAssignment value) => new(
        value.ProductId,
        value.BranchId,
        value.IsActive,
        value.AssignedAtUtc,
        value.AssignedByUserId,
        value.RevokedAtUtc,
        value.RevokedByUserId,
        value.RowVersion);
}
