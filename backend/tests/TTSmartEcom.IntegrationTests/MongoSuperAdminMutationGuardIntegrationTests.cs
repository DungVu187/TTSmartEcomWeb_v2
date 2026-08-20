using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Users;

namespace TTSmartEcom.IntegrationTests;

public sealed class MongoSuperAdminMutationGuardIntegrationTests
{
    private const string ConnectionString =
        "mongodb://127.0.0.1:27017/?serverSelectionTimeoutMS=500";

    [MongoAvailableFact]
    public async Task TryAcquireAsync_WhenRequestsRace_AllowsOneOwnerUntilRelease()
    {
        MongoClient client = new(ConnectionString);
        try
        {
            await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException) { throw new InvalidOperationException("MongoDB không còn khả dụng sau discovery test.", exception); }

        string databaseName = $"TTSV2Guard_{Guid.NewGuid():N}";
        IMongoDatabase database = client.GetDatabase(databaseName);
        try
        {
            MongoSuperAdminMutationGuard guard = new(
                new TestDatabaseProvider(database),
                NullLogger<MongoSuperAdminMutationGuard>.Instance);

            IAsyncDisposable?[] contenders = await Task.WhenAll(
                Enumerable.Range(0, 8)
                    .Select(_ => guard.TryAcquireAsync(CancellationToken.None)));

            IAsyncDisposable winner = Assert.Single(contenders.OfType<IAsyncDisposable>());
            await winner.DisposeAsync();

            await using IAsyncDisposable? next =
                await guard.TryAcquireAsync(CancellationToken.None);
            Assert.NotNull(next);
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
