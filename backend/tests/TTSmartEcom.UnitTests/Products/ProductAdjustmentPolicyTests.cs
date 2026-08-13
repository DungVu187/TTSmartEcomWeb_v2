using MongoDB.Bson;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Products;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductAdjustmentPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("N/A")]
    [InlineData("NA")]
    [InlineData("chua ro")]
    [InlineData("CHUA CO")]
    [InlineData("Chưa phân loại")]
    [InlineData("  Chưa Rõ  ")]
    public void HasRequiredValue_TreatsLegacyPlaceholdersAsMissing(string? value)
    {
        Assert.False(ProductAdjustmentPolicy.HasRequiredValue(value));
    }

    [Fact]
    public void IsAdjusted_UsesOnlyTypeBrandAndSection()
    {
        Assert.True(ProductAdjustmentPolicy.IsAdjusted("PLC", "Siemens", "Automation"));
        Assert.False(ProductAdjustmentPolicy.IsAdjusted("PLC", "Chưa có", "Automation"));
    }

    [Fact]
    public void MapProduct_OverridesStoredAdjustedWithComputedStatus()
    {
        ProductRecord computedTrue = MongoProductCatalogRepository.MapProduct(new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["type"] = "PLC",
            ["brand"] = "Siemens",
            ["section"] = "Automation",
            ["adjusted"] = false,
        }, includePrivate: true);
        ProductRecord computedFalse = MongoProductCatalogRepository.MapProduct(new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["type"] = "PLC",
            ["brand"] = "Chưa rõ",
            ["section"] = "Automation",
            ["adjusted"] = true,
        }, includePrivate: true);

        Assert.False(computedTrue.Adjusted);
        Assert.True(computedTrue.AdjustedStatus);
        Assert.True(computedFalse.Adjusted);
        Assert.False(computedFalse.AdjustedStatus);
    }

    [Fact]
    public void ApplyAdjustedFilter_FiltersBeforePaginationAndUsesComputedStatus()
    {
        ProductRecord[] products =
        [
            Product("1", adjustedStatus: true),
            Product("2", adjustedStatus: false),
            Product("3", adjustedStatus: false),
        ];
        ProductListQuery query = new(
            2, 1, null, null, null, null, null, null,
            "purchaseCount", "desc", true, true, Adjusted: false);

        ProductPage page = MongoProductCatalogRepository.ApplyAdjustedFilter(query, products);

        Assert.Equal(2, page.Total);
        Assert.Equal("3", Assert.Single(page.Products).Id);
    }

    private static ProductRecord Product(string id, bool adjustedStatus) => new(
        id, "PLC", null, null, true, null, null, true, "Siemens", "Automation", null,
        [], null, [], 0, [], 0, 0, 0, null, null, null, null, null, null, null, null, null,
        adjustedStatus);
}
