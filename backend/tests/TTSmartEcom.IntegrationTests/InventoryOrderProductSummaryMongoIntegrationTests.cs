using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Inventory;
using TTSmartEcom.Domain.Inventory;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Inventory;

namespace TTSmartEcom.IntegrationTests;

public sealed class InventoryOrderProductSummaryMongoIntegrationTests
{
    private const string ConnectionString = "mongodb://127.0.0.1:27017/?serverSelectionTimeoutMS=500";

    [MongoAvailableFact]
    public async Task ListProducts_GroupsMixedIds_HandlesCorruptLines_AndKeepsLegacyCount()
    {
        MongoClient client = new(ConnectionString);
        try
        {
            await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        }
        catch (MongoException exception) { throw new InvalidOperationException("MongoDB không còn khả dụng sau discovery test.", exception); }

        string databaseName = $"TTSmartEcomV2InventoryTest_{Guid.NewGuid():N}";
        IMongoDatabase database = client.GetDatabase(databaseName);
        ObjectId firstId = ObjectId.Parse("507f191e810c19729de860ea");
        ObjectId secondId = ObjectId.Parse("507f191e810c19729de860eb");
        ObjectId deletedId = ObjectId.Parse("507f191e810c19729de860ec");
        try
        {
            await database.GetCollection<BsonDocument>("products").InsertManyAsync(
            [
                new BsonDocument { ["_id"] = firstId, ["name"] = "B", ["variant"] = new BsonArray() },
                new BsonDocument { ["_id"] = secondId, ["name"] = "A", ["variant"] = new BsonArray() },
            ]);
            await database.GetCollection<BsonDocument>("iporders").InsertManyAsync(
            [
                Order((firstId.ToString(), 2), (firstId, 3), (secondId.ToString(), "4")),
                Order((deletedId.ToString(), 5), ("not-an-object-id", 99), (BsonNull.Value, 99)),
            ]);

            MongoInventoryOrderRepository repository = new(new TestDatabaseProvider(database));
            (IReadOnlyList<InventoryOrderProductSummary> products, long total) =
                await repository.ListProductsAsync(InventoryOrderKind.Import, 1, CancellationToken.None);

            Assert.Equal(3, total);
            Assert.Collection(products,
                product => { Assert.Equal(secondId.ToString(), product.Id); Assert.Equal(4, product.TotalOrdered); },
                product => { Assert.Equal(firstId.ToString(), product.Id); Assert.Equal(5, product.TotalOrdered); });
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private static BsonDocument Order(params (BsonValue ProductId, BsonValue Quantity)[] lines) => new()
    {
        ["productList"] = new BsonArray(lines.Select(line => new BsonDocument
        {
            ["productId"] = line.ProductId,
            ["quantity"] = line.Quantity,
        })),
    };

    private sealed class TestDatabaseProvider(IMongoDatabase database) : IMongoDatabaseProvider
    {
        public IMongoDatabase Database { get; } = database;
    }
}
