using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Products;

public sealed record ProductVariant(
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
    string? Note,
    bool ContactForPrice = false);

public sealed record ProductInfo(
    string? Manual,
    string? DataSheet,
    string? Catalog,
    string? Others);

public sealed record ProductLink(
    [property: JsonPropertyName("_id")] string? Id,
    string? Label,
    string? Url,
    string? SourceType);

public sealed record ProductReview(
    [property: JsonPropertyName("_id")] string? Id,
    string? Email,
    string? Comment,
    double? Rating,
    DateTimeOffset? CreatedAt);

public sealed record ProductRecord(
    string Id,
    string? Type,
    string? Name,
    string? NameUnsigned,
    bool? Display,
    string? Code,
    string? Vat,
    bool? Adjusted,
    string? Brand,
    string? Section,
    string? Value,
    IReadOnlyList<ProductVariant> Variants,
    ProductInfo? InfoDoc,
    IReadOnlyList<ProductLink> Documents,
    long PurchaseCount,
    IReadOnlyList<ProductReview> Reviews,
    double TotalRating,
    long ReviewCount,
    double AverageReviews,
    string? Warranty,
    string? Solution,
    string? Description,
    string? Features,
    string? OperatingMethod,
    string? Advantages,
    string? Specifications,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool AdjustedStatus);

public sealed record ProductTypeRecord(
    string Id,
    string? Type,
    string? Icon,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);
