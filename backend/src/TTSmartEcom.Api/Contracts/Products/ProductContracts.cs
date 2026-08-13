using System.Text.Json.Serialization;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Api.Contracts.Products;

public sealed class FetchProductsByIdsRequest
{
    [JsonPropertyName("ids")]
    public string[]? Ids { get; set; }
}

public sealed record ProductVariantResponse(
    [property: JsonPropertyName("_id")] string? Id,
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("importPrice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ImportPrice,
    [property: JsonPropertyName("earn"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Earn,
    [property: JsonPropertyName("imgUrl")] string? ImageUrl,
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("shape")] string? Shape,
    [property: JsonPropertyName("buttonCount")] string? ButtonCount,
    [property: JsonPropertyName("frame")] string? Frame,
    [property: JsonPropertyName("quantityForSale")] double? QuantityForSale,
    [property: JsonPropertyName("quantityInStorage")] double? QuantityInStorage,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("contactForPrice")] bool ContactForPrice);

public sealed record ProductResponse(
    [property: JsonPropertyName("_id")] string Id,
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
    [property: JsonPropertyName("variant")] IReadOnlyList<ProductVariantResponse> Variants,
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
    DateTimeOffset? UpdatedAt)
{
    public static ProductResponse From(ProductRecord value) => From(value, value.Adjusted);

    public static ProductResponse FromListing(ProductRecord value) => From(value, value.AdjustedStatus);

    private static ProductResponse From(ProductRecord value, bool? adjusted) => new(
        value.Id, value.Type, value.Name, value.NameUnsigned, value.Display, value.Code, value.Vat,
        adjusted, value.Brand, value.Section, value.Value,
        value.Variants.Select(variant => new ProductVariantResponse(variant.Id, variant.Price, variant.ImportPrice,
            variant.Earn, variant.ImageUrl, variant.Color, variant.Shape, variant.ButtonCount, variant.Frame,
            variant.QuantityForSale, variant.QuantityInStorage, variant.Note, variant.ContactForPrice)).ToArray(),
        value.InfoDoc, value.Documents, value.PurchaseCount, value.Reviews, value.TotalRating, value.ReviewCount,
        value.AverageReviews, value.Warranty, value.Solution, value.Description, value.Features, value.OperatingMethod,
        value.Advantages, value.Specifications, value.CreatedAt, value.UpdatedAt);
}

public sealed record ProductListResponse(long Total, int Page, int Limit, IReadOnlyList<ProductResponse> Products);

public sealed record ProductTypeResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("Type")] string? Type,
    string? Icon,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);
