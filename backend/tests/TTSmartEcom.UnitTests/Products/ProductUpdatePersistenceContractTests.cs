using System.Reflection;
using MongoDB.Bson;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Products;

namespace TTSmartEcom.UnitTests.Products;

/// <summary>
/// These tests exercise the repository's pure BSON builders through reflection.
/// A Mongo integration fixture is intentionally not required: the regression is
/// that the metadata PUT path must not construct a replacement variant array or
/// write inventory fields at all.
/// </summary>
public sealed class ProductUpdatePersistenceContractTests
{
    [Fact]
    public void MetadataUpdateBuilder_WhenVariantsArePresent_DoesNotReplaceVariantArray()
    {
        ProductMutation mutation = MutationWithVariant();

        BsonDocument update = InvokeUpdateBuilder(mutation, includeVariants: false);

        Assert.False(update.Contains("variant"));
        Assert.False(update.Contains("quantityForSale"));
        Assert.False(update.Contains("quantityInStorage"));
        Assert.Equal("Updated product", update["name"].AsString);
    }

    [Fact]
    public void VariantMetadataBuilder_ExcludesInventoryFields()
    {
        ProductVariantMutation mutation = new(
            Id: "507f191e810c19729de860ea",
            Price: "125000",
            ImportPrice: "100000",
            Earn: 25,
            ImageUrl: "/images/product.webp",
            Color: "Đỏ",
            Shape: "Tròn",
            ButtonCount: "1",
            Frame: "Nhựa",
            QuantityForSale: 7,
            QuantityInStorage: 9,
            Note: "metadata");

        BsonDocument metadata = InvokeVariantBuilder(mutation, includeInventory: false);

        Assert.Equal("125000", metadata["price"].AsString);
        Assert.Equal(25, metadata["earn"].AsDouble);
        Assert.False(metadata.Contains("quantityForSale"));
        Assert.False(metadata.Contains("quantityInStorage"));
    }

    [Fact]
    public void DocumentBuilder_PreservesExistingIdAndGeneratesIdForNewLink()
    {
        ObjectId existingId = ObjectId.GenerateNewId();
        ProductMutation mutation = MutationWithVariant() with
        {
            Documents =
            [
                new ProductLinkMutation(existingId.ToString(), "Manual", "/documents/manual.pdf", "file"),
                new ProductLinkMutation(null, "Catalog", "https://example.test/catalog", "link"),
            ],
        };

        BsonArray documents = InvokeUpdateBuilder(mutation, includeVariants: false)["documents"].AsBsonArray;

        Assert.Equal(existingId, documents[0]["_id"].AsObjectId);
        Assert.True(documents[1]["_id"].IsObjectId);
        Assert.NotEqual(existingId, documents[1]["_id"].AsObjectId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-object-id")]
    public void ProductNormalizer_RejectsMalformedDocumentId(string documentId)
    {
        ProductMutation mutation = MutationWithVariant() with
        {
            Documents = [new ProductLinkMutation(documentId, "Manual", "/documents/manual.pdf", "file")],
        };

        Assert.Null(InvokeProductNormalizer(mutation));
    }

    private static ProductMutation MutationWithVariant() => new(
        Type: "PLC",
        Name: "Updated product",
        Code: "PLC-001",
        Brand: "Brand",
        Section: "Section",
        Value: "Value",
        Warranty: "12 months",
        Vat: "10",
        Adjusted: true,
        Display: true,
        Solution: null,
        Description: null,
        Features: null,
        OperatingMethod: null,
        Advantages: null,
        Specifications: null,
        InfoDoc: null,
        Documents: null,
        Variants:
        [
            new ProductVariantMutation(
                Id: "507f191e810c19729de860ea",
                Price: "125000",
                ImportPrice: "100000",
                Earn: 25,
                ImageUrl: null,
                Color: "Đỏ",
                Shape: null,
                ButtonCount: null,
                Frame: null,
                QuantityForSale: 7,
                QuantityInStorage: 9,
                Note: null),
        ]);

    private static BsonDocument InvokeUpdateBuilder(ProductMutation mutation, bool includeVariants)
    {
        MethodInfo method = typeof(MongoProductCatalogWriteRepository).GetMethod(
            "ToUpdateDocument",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return Assert.IsType<BsonDocument>(method.Invoke(null, [mutation, includeVariants]));
    }

    private static BsonDocument InvokeVariantBuilder(ProductVariantMutation mutation, bool includeInventory)
    {
        MethodInfo method = typeof(MongoProductCatalogWriteRepository).GetMethod(
            "ToVariantDocument",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return Assert.IsType<BsonDocument>(method.Invoke(null, [mutation, includeInventory]));
    }

    private static ProductMutation? InvokeProductNormalizer(ProductMutation mutation)
    {
        MethodInfo method = typeof(ProductCatalogWriteService).GetMethod(
            "NormalizeProduct",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return method.Invoke(null, [mutation, false]) as ProductMutation;
    }
}
