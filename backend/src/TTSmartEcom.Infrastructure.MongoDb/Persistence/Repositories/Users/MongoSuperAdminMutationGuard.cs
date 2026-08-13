using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Users;

/// <summary>
/// Uses MongoDB's always-unique _id as a fail-closed distributed mutex. The guard
/// intentionally has no automatic expiry: losing a process cannot reopen the race
/// and create a second superadmin. Operations must inspect users before manually
/// clearing an orphaned guard.
/// </summary>
public sealed partial class MongoSuperAdminMutationGuard(
    IMongoDatabaseProvider databaseProvider,
    ILogger<MongoSuperAdminMutationGuard> logger) : ISuperAdminMutationGuard
{
    internal const string GuardId = "__ttsmart_v2_superadmin_mutation_guard";
    private readonly IMongoCollection<BsonDocument> counters =
        databaseProvider.Database.GetCollection<BsonDocument>(CounterDocument.CollectionName);

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        string owner = Guid.NewGuid().ToString("N");
        BsonDocument document = new()
        {
            ["_id"] = GuardId,
            ["id"] = "superAdminMutationGuard",
            ["owner"] = owner,
            ["createdAt"] = DateTime.UtcNow,
        };
        try
        {
            await counters.InsertOneAsync(document, cancellationToken: cancellationToken);
            return new GuardHandle(counters, owner, logger);
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            LogContention(logger);
            return null;
        }
    }

    [LoggerMessage(
        EventId = 1291,
        Level = LogLevel.Warning,
        Message = "A superadmin mutation was rejected because the distributed guard is already held")]
    private static partial void LogContention(ILogger logger);

    [LoggerMessage(
        EventId = 1292,
        Level = LogLevel.Error,
        Message = "The superadmin mutation guard could not be released; manual inspection is required")]
    private static partial void LogReleaseFailure(ILogger logger);

    private sealed class GuardHandle(
        IMongoCollection<BsonDocument> counters,
        string owner,
        ILogger logger) : IAsyncDisposable
    {
        private int disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            try
            {
                await counters.DeleteOneAsync(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("_id", GuardId),
                        Builders<BsonDocument>.Filter.Eq("owner", owner)),
                    CancellationToken.None);
            }
            catch (MongoException)
            {
                LogReleaseFailure(logger);
            }
        }
    }
}
