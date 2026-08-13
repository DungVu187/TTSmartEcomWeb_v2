using MongoDB.Bson;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Inventory;

namespace TTSmartEcom.UnitTests.Inventory;

public sealed class InventoryOrderProductSummaryAggregationTests
{
    [Fact]
    public void Build_NormalizesMixedIds_JoinsBeforeCounting_AndPaginatesOnce()
    {
        BsonDocument[] pipeline = InventoryOrderProductSummaryAggregation.Build(3);

        Assert.Equal("$productList", pipeline[0]["$unwind"]);
        BsonDocument conversion = pipeline[1]["$set"]["normalizedProductId"]["$convert"].AsBsonDocument;
        Assert.Equal("$productList.productId", conversion["input"]);
        Assert.Equal("objectId", conversion["to"]);
        Assert.True(conversion["onError"].IsBsonNull);
        Assert.True(conversion["onNull"].IsBsonNull);
        Assert.Equal("$normalizedProductId", pipeline[3]["$group"]["_id"]);

        BsonDocument facet = pipeline[^1]["$facet"].AsBsonDocument;
        BsonArray productStages = facet["products"].AsBsonArray;
        BsonDocument sort = productStages.Single(stage => stage.IsBsonDocument && stage.AsBsonDocument.Contains("$sort"))["$sort"].AsBsonDocument;
        Assert.Equal(1, sort["name"].ToInt32());
        Assert.Equal(1, sort["_id"].ToInt32());

        Assert.Equal(20, productStages[^2]["$skip"].ToInt32());
        Assert.Equal(10, productStages[^1]["$limit"].ToInt32());
        Assert.Equal("total", facet["metadata"][0]["$count"]);
        Assert.Contains(productStages, stage => stage.AsBsonDocument.Contains("$lookup"));
        Assert.Contains(productStages, stage => stage.IsBsonDocument && stage.AsBsonDocument.TryGetValue("$unwind", out BsonValue value) && value == "$productInfo");
    }

    [Fact]
    public void Map_HandlesLegacyNullsMissingFieldsAndMixedNumericTypes()
    {
        ObjectId productId = ObjectId.Parse("507f191e810c19729de860ea");
        ObjectId variantId = ObjectId.Parse("507f191e810c19729de860eb");
        BsonDocument facet = new()
        {
            ["products"] = new BsonArray
            {
                new BsonDocument
                {
                    ["_id"] = productId,
                    ["name"] = "San pham tong hop",
                    ["brand"] = BsonNull.Value,
                    ["variant"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["_id"] = variantId,
                            ["price"] = "100000",
                            ["imgUrl"] = BsonNull.Value,
                            ["earn"] = 25,
                            ["quantityForSale"] = 4L,
                        },
                    },
                    ["totalOrdered"] = 7.5,
                },
            },
            ["metadata"] = new BsonArray { new BsonDocument("total", 12L) },
        };

        (IReadOnlyList<Domain.Inventory.InventoryOrderProductSummary> products, long total) =
            InventoryOrderProductSummaryAggregation.Map(facet);

        Domain.Inventory.InventoryOrderProductSummary product = Assert.Single(products);
        Assert.Equal(12, total);
        Assert.Equal(productId.ToString(), product.Id);
        Assert.Equal("San pham tong hop", product.Name);
        Assert.Null(product.Brand);
        Assert.Equal(7.5, product.TotalOrdered);
        Domain.Inventory.InventoryOrderProductVariant variant = Assert.Single(product.Variants);
        Assert.Equal(variantId.ToString(), variant.Id);
        Assert.Equal("100000", variant.Price);
        Assert.Null(variant.ImageUrl);
        Assert.Equal(25, variant.Earn);
        Assert.Equal(4, variant.QuantityForSale);
        Assert.Null(variant.QuantityInStorage);
    }

    [Fact]
    public void Map_WhenFacetIsEmpty_ReturnsEmptyPage()
    {
        (IReadOnlyList<Domain.Inventory.InventoryOrderProductSummary> products, long total) =
            InventoryOrderProductSummaryAggregation.Map(null);

        Assert.Empty(products);
        Assert.Equal(0, total);
    }
}
