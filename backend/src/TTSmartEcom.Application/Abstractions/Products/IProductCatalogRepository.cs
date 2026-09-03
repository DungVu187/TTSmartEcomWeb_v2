using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Application.Abstractions.Products;

public sealed record ProductListQuery(
    int Page,
    int Limit,
    string? Search,
    string? Code,
    string? Type,
    string? Brand,
    string? Section,
    string? Value,
    string SortBy,
    string SortOrder,
    bool? Display,
    bool IncludePrivate,
    IReadOnlyCollection<string>? AllowedProductIds = null,
    bool? Adjusted = null,
    Guid? BranchId = null,
    Guid? CompanyId = null);

public sealed record ProductPage(
    long Total,
    int Page,
    int Limit,
    IReadOnlyList<ProductRecord> Products);

public sealed record ProductVariantMutation(
    string? Id,
    string? Price,
    string? ImportPrice,
    double? Earn,
    string? ImageUrl,
    string? Color,
    string? Shape,
    string? ButtonCount,
    string? Frame,
    double? QuantityForSale,
    double? QuantityInStorage,
    string? Note);

public sealed record ProductInfoMutation(string? Manual, string? DataSheet, string? Catalog, string? Others);

public sealed record ProductLinkMutation(string? Id, string? Label, string? Url, string? SourceType);

public sealed record ProductMutation(
    string? Type,
    string? Name,
    string? Code,
    string? Brand,
    string? Section,
    string? Value,
    string? Warranty,
    string? Vat,
    bool? Adjusted,
    bool? Display,
    string? Solution,
    string? Description,
    string? Features,
    string? OperatingMethod,
    string? Advantages,
    string? Specifications,
    ProductInfoMutation? InfoDoc,
    IReadOnlyList<ProductLinkMutation>? Documents,
    IReadOnlyList<ProductVariantMutation>? Variants);

public sealed record ProductCreationAssignment(
    Guid CompanyId,
    Guid? BranchId,
    Guid? ActorUserId,
    string ActorName);

public enum ProductMutationStatus
{
    Success,
    NotFound,
    Conflict,
    Invalid,
    Forbidden,
}

public sealed record ProductMutationResult(
    ProductMutationStatus Status,
    ProductRecord? Product = null,
    ProductVariant? Variant = null,
    long AffectedCount = 0,
    string? Message = null);

public sealed record ProductTypeMutationResult(
    ProductMutationStatus Status,
    ProductTypeRecord? ProductType = null,
    long UpdatedProducts = 0,
    long UpdatedHomeCategories = 0,
    string? Message = null);

public sealed record ProductReviewMutationResult(
    ProductMutationStatus Status,
    ProductReview? Review = null,
    ProductRecord? Product = null,
    string? Message = null);

public sealed record ProductStockMutation(
    double Quantity,
    string? UserName,
    string? OrderId,
    string? OrderName,
    bool IsAiScan);

public sealed record ProductStockMutationResult(
    ProductMutationStatus Status,
    ProductRecord? Product = null,
    string? HistoryId = null,
    string? Message = null);

public sealed record ProductVariantImageReference(bool VariantExists, string? ImageUrl);

public sealed record ProductMediaMutationResult(
    ProductMutationStatus Status,
    ProductRecord? Product = null,
    string? ImageUrl = null,
    string? Message = null);

public interface IProductCatalogRepository
{
    Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken);

    Task<ProductRecord?> FindByIdAsync(string id, bool includePrivate, CancellationToken cancellationToken);

    Task<ProductRecord?> FindByIdAsync(
        string id,
        bool includePrivate,
        Guid? branchId,
        CancellationToken cancellationToken) =>
        FindByIdAsync(id, includePrivate, cancellationToken);

    Task<ProductRecord?> FindByIdAsync(
        string id,
        bool includePrivate,
        Guid? companyId,
        Guid? branchId,
        CancellationToken cancellationToken) =>
        FindByIdAsync(id, includePrivate, branchId, cancellationToken);

    Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
        IReadOnlyCollection<string> ids,
        bool includePrivate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
        IReadOnlyCollection<string> ids,
        bool includePrivate,
        Guid? branchId,
        CancellationToken cancellationToken) =>
        FindByIdsAsync(ids, includePrivate, cancellationToken);

    Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
        IReadOnlyCollection<string> ids,
        bool includePrivate,
        Guid? companyId,
        Guid? branchId,
        CancellationToken cancellationToken) =>
        FindByIdsAsync(ids, includePrivate, branchId, cancellationToken);

    Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(
        Guid? companyId,
        CancellationToken cancellationToken) =>
        ListTypesAsync(cancellationToken);
}

public interface IProductCatalogWriteRepository
{

    Task<ProductRecord?> FindEquivalentCodeAsync(string normalizedCode, string? excludeId, CancellationToken cancellationToken);

    Task<ProductRecord?> FindEquivalentCodeAsync(
        string normalizedCode,
        string? excludeId,
        Guid? companyId,
        CancellationToken cancellationToken) =>
        FindEquivalentCodeAsync(normalizedCode, excludeId, cancellationToken);

    Task<ProductMutationResult> CreateAsync(ProductMutation product, CancellationToken cancellationToken);

    Task<ProductMutationResult> CreateAsync(
        ProductMutation product,
        ProductCreationAssignment? assignment,
        CancellationToken cancellationToken) =>
        CreateAsync(product, cancellationToken);

    Task<ProductMutationResult> UpdateAsync(string id, ProductMutation product, CancellationToken cancellationToken);

    Task<ProductMutationResult> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<ProductMutationResult> ToggleDisplayAsync(string id, CancellationToken cancellationToken);

    Task<ProductMutationResult> DeleteManyAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    Task<ProductMutationResult> AddVariantAsync(string productId, ProductVariantMutation variant, CancellationToken cancellationToken);

    Task<ProductMutationResult> UpdateVariantAsync(string productId, int variantIndex, ProductVariantMutation variant, CancellationToken cancellationToken);

    Task<ProductMutationResult> DeleteVariantAsync(string productId, int variantIndex, CancellationToken cancellationToken);

    Task<ProductMutationResult> UpdateVariantEarnAsync(string productId, int variantIndex, double earn, CancellationToken cancellationToken);

    Task<ProductMutationResult> UpdateVariantImportPriceAsync(string productId, int variantIndex, string importPrice, CancellationToken cancellationToken);

    Task<ProductMutationResult> AdjustPurchaseCountAsync(string productId, long delta, CancellationToken cancellationToken);

    Task<long> BackfillDisplayAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductRecord>> FindByCodesAsync(
        IReadOnlyCollection<string> codes,
        bool includePrivate,
        CancellationToken cancellationToken);

    Task<ProductTypeMutationResult> CreateTypeAsync(string name, string icon, CancellationToken cancellationToken);

    Task<ProductTypeMutationResult> UpdateTypeAsync(string id, string name, string icon, CancellationToken cancellationToken);

    Task<ProductTypeMutationResult> DeleteTypeAsync(string id, bool requireUnused, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductReview>?> GetReviewsAsync(string productId, CancellationToken cancellationToken);

    Task<ProductReviewMutationResult> CreateReviewAsync(
        string productId, string email, string? comment, double rating, CancellationToken cancellationToken);

    Task<ProductReviewMutationResult> UpdateReviewAsync(
        string productId, string reviewId, string? comment, double? rating,
        string actorEmail, bool isModerator, CancellationToken cancellationToken);

    Task<ProductReviewMutationResult> DeleteReviewAsync(
        string productId, string reviewId, string actorEmail, bool isModerator, CancellationToken cancellationToken);

    Task<ProductStockMutationResult> AdjustStockAsync(
        string productId, int variantIndex, ProductStockMutation mutation, CancellationToken cancellationToken);
}

public interface IProductMediaRepository
{
    Task<ProductVariantImageReference?> GetVariantImageReferenceAsync(
        string productId,
        int variantIndex,
        CancellationToken cancellationToken);

    Task<bool> IsProductImageReferencedElsewhereAsync(
        string productId,
        int variantIndex,
        string filename,
        CancellationToken cancellationToken);

    Task<ProductRecord?> ClearVariantImageAsync(
        string productId,
        int variantIndex,
        string expectedImageUrl,
        CancellationToken cancellationToken);

    Task<bool> IsInvoiceImageReferencedAsync(string filename, CancellationToken cancellationToken);
}
