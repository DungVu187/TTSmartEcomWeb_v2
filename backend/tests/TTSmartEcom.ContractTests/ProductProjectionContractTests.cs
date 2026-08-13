using System.Text.Json;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.ContractTests;

public sealed class ProductProjectionContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ProductResponse_UsesLegacyUnderscoreIdsForEmbeddedDocuments()
    {
        ProductRecord product = new(
            Id: "507f191e810c19729de860ea",
            Type: "PLC",
            Name: "Sản phẩm",
            NameUnsigned: null,
            Display: true,
            Code: "PLC-001",
            Vat: "10",
            Adjusted: true,
            Brand: "Hãng",
            Section: "Cụm",
            Value: "Thiết bị",
            Variants: [new ProductVariant("507f191e810c19729de860eb", "100", "80", 25, null, null, null, null, null, 1, 2, null)],
            InfoDoc: null,
            Documents: [new ProductLink("507f191e810c19729de860ec", "Manual", "/documents/manual.pdf", "file")],
            PurchaseCount: 0,
            Reviews: [new ProductReview("507f191e810c19729de860ed", "customer@example.test", "Tốt", 5, null)],
            TotalRating: 5,
            ReviewCount: 1,
            AverageReviews: 5,
            Warranty: "12 tháng",
            Solution: null,
            Description: null,
            Features: null,
            OperatingMethod: null,
            Advantages: null,
            Specifications: null,
            CreatedAt: null,
            UpdatedAt: null,
            AdjustedStatus: false);

        string json = JsonSerializer.Serialize(ProductResponse.From(product), WebJson);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal("507f191e810c19729de860ec", root.GetProperty("documents")[0].GetProperty("_id").GetString());
        Assert.Equal("507f191e810c19729de860ed", root.GetProperty("reviews")[0].GetProperty("_id").GetString());
        Assert.False(root.GetProperty("documents")[0].TryGetProperty("id", out _));
        Assert.False(root.GetProperty("reviews")[0].TryGetProperty("id", out _));
        Assert.True(root.GetProperty("adjusted").GetBoolean());
        Assert.False(root.TryGetProperty("adjustedStatus", out _));
    }

    [Fact]
    public void ProductListingResponse_UsesComputedAdjustedValueWithoutAddingNonLegacyField()
    {
        ProductRecord product = Product(adjusted: true, adjustedStatus: false);

        string json = JsonSerializer.Serialize(ProductResponse.FromListing(product), WebJson);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.GetProperty("adjusted").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("adjustedStatus", out _));
    }

    private static ProductRecord Product(bool adjusted, bool adjustedStatus) => new(
        Id: "507f191e810c19729de860ea", Type: "PLC", Name: "Sản phẩm", NameUnsigned: null,
        Display: true, Code: "PLC-001", Vat: "10", Adjusted: adjusted, Brand: "Hãng",
        Section: "Cụm", Value: "Thiết bị", Variants: [], InfoDoc: null, Documents: [],
        PurchaseCount: 0, Reviews: [], TotalRating: 0, ReviewCount: 0, AverageReviews: 0,
        Warranty: null, Solution: null, Description: null, Features: null, OperatingMethod: null,
        Advantages: null, Specifications: null, CreatedAt: null, UpdatedAt: null,
        AdjustedStatus: adjustedStatus);
}
