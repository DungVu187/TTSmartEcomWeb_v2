using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Products;

public sealed class ProductBranchDistributionService(
    IProductBranchAssignmentRepository assignments,
    ICompanyBranchDirectory branches,
    IAccessScopeService accessScope)
{
    private const int MaxProducts = 200;
    private const int MaxBranches = 100;
    private const string RequiredPermission = "product.edit";

    public Task<IReadOnlyList<ActiveCompanyBranch>> ListActiveBranchesAsync(
        ICurrentUserContext context,
        CancellationToken cancellationToken)
    {
        Guid companyId = RequireCompanyScope(context);
        return branches.ListActiveBranchesAsync(companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductBranchDistributionStatus>> GetDistributionStatusAsync(
        IReadOnlyCollection<string>? productIds,
        ICurrentUserContext context,
        CancellationToken cancellationToken)
    {
        Guid companyId = RequireCompanyScope(context);
        string[] normalizedProducts = NormalizeProductIds(productIds);
        IReadOnlyList<ActiveCompanyBranch> activeBranches = await branches.ListActiveBranchesAsync(companyId, cancellationToken);
        return await assignments.GetDistributionStatusAsync(
            companyId, normalizedProducts, activeBranches.Select(branch => branch.BranchId).ToArray(), cancellationToken);
    }

    public async Task<IReadOnlyList<ProductBranchAssignment>> ListAsync(
        string productId,
        ICurrentUserContext context,
        CancellationToken cancellationToken)
    {
        Guid companyId = RequireCompanyScope(context);
        string normalizedProductId = RequireProductId(productId);
        ProductBranchAssignmentQueryResult result;
        try
        {
            result = await assignments.ListForProductAsync(
                companyId, normalizedProductId, cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw Error(403, "Company database không khớp phạm vi đã xác thực.", exception);
        }
        if (!result.ProductExists) throw Error(404, "Không tìm thấy sản phẩm trong công ty hiện tại.");
        return result.Assignments;
    }

    public async Task<ProductCreationAssignment> ResolveCreationAssignmentAsync(
        ICurrentUserContext context,
        CancellationToken cancellationToken)
    {
        Guid companyId = RequireCompanyScope(context, "product.create");
        if (!context.ActiveBranchId.HasValue)
            return new ProductCreationAssignment(companyId, null, context.UserId, ActorName(context));

        Guid branchId = context.ActiveBranchId.Value;
        if (branchId == Guid.Empty || !accessScope.CanAccessBranch(context, branchId) ||
            !accessScope.IsInScope(context, companyId, branchId))
            throw Error(403, "Chi nhánh hiện tại không thuộc phạm vi đã xác thực.");

        await ValidateBranchesAsync(companyId, [branchId], cancellationToken);
        return new ProductCreationAssignment(
            companyId,
            branchId,
            context.UserId,
            ActorName(context));
    }

    public async Task<bool> IsActiveAsync(
        string productId,
        Guid branchId,
        ICurrentUserContext context,
        CancellationToken cancellationToken)
    {
        Guid companyId = RequireCompanyScope(context);
        string normalizedProductId = RequireProductId(productId);
        await ValidateBranchesAsync(companyId, [branchId], cancellationToken);
        bool? active;
        try
        {
            active = await assignments.IsActiveAsync(companyId, normalizedProductId, branchId, cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw Error(403, "Company database không khớp phạm vi đã xác thực.", exception);
        }
        if (!active.HasValue) throw Error(404, "Không tìm thấy sản phẩm trong công ty hiện tại.");
        return active.Value;
    }

    public Task<ProductBranchAssignmentChange> AssignAsync(
        IReadOnlyCollection<string>? productIds,
        IReadOnlyCollection<Guid>? branchIds,
        ICurrentUserContext context,
        CancellationToken cancellationToken) =>
        SetActiveAsync(productIds, branchIds, true, context, cancellationToken);

    public Task<ProductBranchAssignmentChange> RevokeAsync(
        IReadOnlyCollection<string>? productIds,
        IReadOnlyCollection<Guid>? branchIds,
        ICurrentUserContext context,
        CancellationToken cancellationToken) =>
        SetActiveAsync(productIds, branchIds, false, context, cancellationToken);

    private async Task<ProductBranchAssignmentChange> SetActiveAsync(
        IReadOnlyCollection<string>? productIds,
        IReadOnlyCollection<Guid>? branchIds,
        bool isActive,
        ICurrentUserContext context,
        CancellationToken cancellationToken)
    {
        Guid companyId = RequireCompanyScope(context);
        string[] normalizedProducts = NormalizeProductIds(productIds);
        Guid[] normalizedBranches = NormalizeBranchIds(branchIds);
        await ValidateBranchesAsync(companyId, normalizedBranches, cancellationToken);

        ProductBranchAssignmentMutationResult result;
        try
        {
            result = await assignments.SetActiveAsync(
                companyId,
                normalizedProducts,
                normalizedBranches,
                isActive,
                context.UserId,
                ActorName(context),
                cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw Error(403, "Company database không khớp phạm vi đã xác thực.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw Error(409, "Phân phối sản phẩm vừa được thay đổi bởi yêu cầu khác.", exception);
        }

        if (!result.ProductsExist || result.MissingProductIds.Count > 0)
        {
            throw Error(404, "Có sản phẩm không tồn tại trong công ty hiện tại.");
        }

        if (result.ChangedCount == 0)
            throw Error(409, isActive
                ? "Các sản phẩm đã được phân phối đầy đủ tới chi nhánh đã chọn."
                : "Các sản phẩm chưa được phân phối tới chi nhánh đã chọn.");

        return new ProductBranchAssignmentChange(normalizedProducts, normalizedBranches, result.ChangedCount);
    }

    private Guid RequireCompanyScope(ICurrentUserContext context, string requiredPermission = RequiredPermission)
    {
        if (context is null || !context.IsAuthenticated) throw Error(403, "Yêu cầu xác thực.");
        if (!context.ActiveCompanyId.HasValue || context.ActiveCompanyId.Value == Guid.Empty)
            throw Error(400, "Phải chọn phạm vi công ty trước khi phân phối sản phẩm.");
        Guid companyId = context.ActiveCompanyId.Value;
        if (!accessScope.CanAccessCompany(context, companyId))
            throw Error(403, "Không có quyền truy cập công ty hiện tại.");
        if (!accessScope.HasCompanyPermission(context, companyId, requiredPermission))
            throw Error(403, "Không có quyền phân phối sản phẩm cấp công ty.");
        return companyId;
    }

    private async Task ValidateBranchesAsync(
        Guid companyId,
        Guid[] branchIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, BranchCompanyReference> found = await branches.FindBranchesAsync(
            branchIds, cancellationToken);
        if (found.Count != branchIds.Length) throw Error(404, "Có chi nhánh không tồn tại.");
        if (found.Values.Any(branch => !branch.IsActive)) throw Error(409, "Có chi nhánh chưa hoạt động.");
        if (found.Values.Any(branch => branch.CompanyId != companyId))
            throw Error(403, "Không thể phân phối sản phẩm sang chi nhánh thuộc công ty khác.");
    }

    private static string RequireProductId(string productId)
    {
        string value = productId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!IsPublicId(value)) throw Error(400, "Mã sản phẩm không hợp lệ.");
        return value;
    }

    private static string[] NormalizeProductIds(IReadOnlyCollection<string>? productIds)
    {
        if (productIds is null || productIds.Count == 0 || productIds.Count > MaxProducts)
            throw Error(400, $"Danh sách sản phẩm phải có từ 1 đến {MaxProducts} phần tử.");
        string[] values = productIds.Select(RequireProductId).Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0) throw Error(400, "Danh sách sản phẩm không hợp lệ.");
        return values;
    }

    private static Guid[] NormalizeBranchIds(IReadOnlyCollection<Guid>? branchIds)
    {
        if (branchIds is null || branchIds.Count == 0 || branchIds.Count > MaxBranches || branchIds.Any(id => id == Guid.Empty))
            throw Error(400, $"Danh sách chi nhánh phải có từ 1 đến {MaxBranches} phần tử hợp lệ.");
        return branchIds.Distinct().ToArray();
    }

    private static bool IsPublicId(string value) =>
        value.Length == 24 && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string ActorName(ICurrentUserContext context) =>
        string.IsNullOrWhiteSpace(context.DisplayName) ? context.Email ?? "system" : context.DisplayName;

    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(
        int status,
        string message,
        Exception? inner = null) =>
        new(new ApplicationError($"TTS-PRODUCT-DISTRIBUTION-{status}", 4500 + status, status, message), inner);
}
