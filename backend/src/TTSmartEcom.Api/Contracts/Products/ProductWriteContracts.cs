using System.Text.Json.Serialization;
using TTSmartEcom.Application.Abstractions.Products;

namespace TTSmartEcom.Api.Contracts.Products;

public sealed class ProductTypeMutationRequest
{
    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

public sealed class ProductInfoMutationRequest
{
    public string? Manual { get; set; }
    public string? DataSheet { get; set; }
    public string? Catalog { get; set; }
    public string? Others { get; set; }
}

public sealed class ProductLinkMutationRequest
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Label { get; set; }
    public string? Url { get; set; }
    public string? SourceType { get; set; }
}

public sealed class ProductVariantMutationRequest
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    public string? Price { get; set; }
    public string? ImportPrice { get; set; }
    [JsonConverter(typeof(LegacyOptionalEarnConverter))]
    public double? Earn { get; set; }
    [JsonPropertyName("imgUrl")]
    public string? ImageUrl { get; set; }
    public string? Color { get; set; }
    public string? Shape { get; set; }
    public string? ButtonCount { get; set; }
    public string? Frame { get; set; }
    public double? QuantityForSale { get; set; }
    public double? QuantityInStorage { get; set; }
    public string? Note { get; set; }

    public ProductVariantMutation ToMutation() => new(
        Id, Price, ImportPrice, Earn, ImageUrl, Color, Shape, ButtonCount, Frame,
        QuantityForSale, QuantityInStorage, Note);
}

public sealed class ProductMutationRequest
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Brand { get; set; }
    public string? Section { get; set; }
    public string? Value { get; set; }
    public string? Warranty { get; set; }
    public string? Waranty { get; set; }
    public string? Vat { get; set; }
    public bool? Adjusted { get; set; }
    public bool? Display { get; set; }
    public string? Solution { get; set; }
    public string? Description { get; set; }
    public string? Features { get; set; }
    public string? OperatingMethod { get; set; }
    public string? Advantages { get; set; }
    public string? Specifications { get; set; }
    public ProductInfoMutationRequest? InfoDoc { get; set; }
    public List<ProductLinkMutationRequest>? Documents { get; set; }
    [JsonPropertyName("variant")]
    public List<ProductVariantMutationRequest>? Variants { get; set; }

    public ProductMutation ToMutation() => new(
        Type, Name, Code, Brand, Section, Value, Warranty ?? Waranty, Vat, Adjusted, Display,
        Solution, Description, Features, OperatingMethod, Advantages, Specifications,
        InfoDoc is null ? null : new ProductInfoMutation(InfoDoc.Manual, InfoDoc.DataSheet, InfoDoc.Catalog, InfoDoc.Others),
        Documents?.Select(value => new ProductLinkMutation(value.Id, value.Label, value.Url, value.SourceType)).ToArray(),
        Variants?.Select(value => value.ToMutation()).ToArray());
}

public sealed record ProductBulkDeleteRequest(string[]? Ids);
public sealed record ProductCodesRequest(string[]? Codes);
public sealed record ProductPurchaseRequest(string? Action, long Amount);
public sealed record ProductEarnRequest(double Earn);
public sealed record ProductImportPriceRequest(string? ImportPrice);

public sealed record ProductReviewRequest(string? Comment, double? Rating);

public sealed class ProductStockRequest
{
    public System.Text.Json.JsonElement Quantity { get; set; }
    public System.Text.Json.JsonElement OrderId { get; set; }
    public System.Text.Json.JsonElement OrderName { get; set; }
    public System.Text.Json.JsonElement IsAiScan { get; set; }
}
