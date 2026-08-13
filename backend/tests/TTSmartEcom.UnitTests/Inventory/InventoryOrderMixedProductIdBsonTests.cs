using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.UnitTests.Inventory;

public sealed class InventoryOrderMixedProductIdBsonTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InventoryOrderLine_ReadsStringAndObjectId_ButWritesLegacyString(bool importOrder)
    {
        ObjectId productId = ObjectId.Parse("507f191e810c19729de860ea");
        BsonDocument fixture = new()
        {
            ["_id"] = ObjectId.Parse("507f1f77bcf86cd799439011"),
            ["productList"] = new BsonArray
            {
                new BsonDocument
                {
                    ["_id"] = ObjectId.Parse("507f191e810c19729de860eb"),
                    ["productId"] = productId,
                    ["quantity"] = 1,
                },
            },
        };

        BsonDocument roundTrip;
        if (importOrder)
        {
            IpOrderDocument document = BsonSerializer.Deserialize<IpOrderDocument>(fixture);
            Assert.Equal(productId.ToString(), Assert.Single(document.ProductList!).ProductId);
            roundTrip = document.ToBsonDocument();
        }
        else
        {
            EpOrderDocument document = BsonSerializer.Deserialize<EpOrderDocument>(fixture);
            Assert.Equal(productId.ToString(), Assert.Single(document.ProductList!).ProductId);
            roundTrip = document.ToBsonDocument();
        }

        BsonValue written = roundTrip["productList"][0]["productId"];
        Assert.True(written.IsString);
        Assert.Equal(productId.ToString(), written.AsString);
    }
}
