using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Integrations;

public sealed class MongoZaloOrderCredentialRepository(
    IMongoDatabaseProvider databaseProvider) : IZaloOrderCredentialRepository
{
    private readonly IMongoCollection<ZaloConfigDocument> collection =
        databaseProvider.Database.GetCollection<ZaloConfigDocument>(ZaloConfigDocument.CollectionName);

    public async Task<ZaloOrderDeliveryCredentials?> FindAsync(
        CancellationToken cancellationToken)
    {
        ZaloConfigDocument? document = await collection
            .Find(Builders<ZaloConfigDocument>.Filter.Empty)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null) return null;

        return new ZaloOrderDeliveryCredentials(
            document.Id.ToString(),
            document.Version ?? 0,
            document.AppId ?? string.Empty,
            document.SecretKey ?? string.Empty,
            document.RecipientUserId ?? string.Empty,
            document.AccessToken ?? string.Empty,
            document.RefreshToken ?? string.Empty,
            document.ExpiresAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(document.ExpiresAt.Value, DateTimeKind.Utc))
                : null);
    }

    public async Task<bool> TryUpdateTokensAsync(
        string configurationId,
        int expectedVersion,
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(configurationId, out ObjectId id)) return false;
        FilterDefinitionBuilder<ZaloConfigDocument> filters = Builders<ZaloConfigDocument>.Filter;
        FilterDefinition<ZaloConfigDocument> version = expectedVersion == 0
            ? filters.Or(
                filters.Eq("__v", 0),
                filters.Exists("__v", false),
                filters.Eq("__v", BsonNull.Value))
            : filters.Eq(x => x.Version, expectedVersion);
        UpdateResult result = await collection.UpdateOneAsync(
            filters.And(filters.Eq(x => x.Id, id), version),
            Builders<ZaloConfigDocument>.Update
                .Set(x => x.AccessToken, accessToken)
                .Set(x => x.RefreshToken, refreshToken)
                .Set(x => x.ExpiresAt, expiresAt.UtcDateTime)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Set(x => x.Version, checked(expectedVersion + 1)),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
