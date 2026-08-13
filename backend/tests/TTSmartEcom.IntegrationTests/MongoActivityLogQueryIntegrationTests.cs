using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Audit;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Audit;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class MongoActivityLogQueryIntegrationTests
{
    private const string ConnectionString = "mongodb://localhost:27017/?serverSelectionTimeoutMS=500";

    [Fact]
    public async Task QueryAsync_ResolvesProductAndStationReferencesUsingLegacyLabels()
    {
        MongoClient client = new(ConnectionString);
        try
        {
            await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw SkipException.ForSkip(
                "MongoDB local không khả dụng; chưa chạy integration ActivityLog trên database biệt lập.");
        }

        string databaseName = $"TTSmartEcomV2ActivityLogTest_{Guid.NewGuid():N}";
        IMongoDatabase database = client.GetDatabase(databaseName);
        ObjectId productId = ObjectId.Parse("507f191e810c19729de860ea");
        ObjectId stationId = ObjectId.Parse("507f191e810c19729de860eb");
        ObjectId logId = ObjectId.Parse("507f191e810c19729de860ec");
        try
        {
            await database.GetCollection<BsonDocument>("products").InsertOneAsync(new BsonDocument
            {
                ["_id"] = productId,
                ["code"] = "SP-001",
                ["name"] = "Sản phẩm kiểm thử",
            });
            await database.GetCollection<BsonDocument>("stations").InsertOneAsync(new BsonDocument
            {
                ["_id"] = stationId,
                ["stationCode"] = "T01",
                ["stationName"] = "Trạm 01",
            });
            await database.GetCollection<BsonDocument>("activitylogs").InsertOneAsync(new BsonDocument
            {
                ["_id"] = logId,
                ["userName"] = "Quản trị viên",
                ["action"] = "add_chip_attr",
                ["details"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["field"] = "productId",
                        ["oldValue"] = string.Empty,
                        ["newValue"] = productId.ToString(),
                    },
                    new BsonDocument
                    {
                        ["field"] = "station",
                        ["oldValue"] = stationId.ToString(),
                        ["newValue"] = string.Empty,
                    },
                },
                ["createdAt"] = DateTime.UtcNow,
            });

            MongoAuditRepository repository = new(new TestDatabaseProvider(database));
            ActivityLogPage page = await repository.QueryAsync(
                new ActivityLogQuery(1, 20, null, null, null, null, null),
                CancellationToken.None);

            ActivityLog log = Assert.Single(page.Logs);
            Assert.Equal(logId.ToString(), log.Id);
            Assert.Equal("SP-001", page.References!.Products[productId.ToString()]);
            Assert.Equal("T01 - Trạm 01", page.References.Stations[stationId.ToString()]);
            Assert.Equal("Thêm thuộc tính sản phẩm", page.ActionLabels["add_chip_attr"]);
            Assert.Equal("Xóa thuộc tính sản phẩm", page.ActionLabels["remove_chip_attr"]);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private sealed class TestDatabaseProvider(IMongoDatabase database) : IMongoDatabaseProvider
    {
        public IMongoDatabase Database { get; } = database;
    }
}
