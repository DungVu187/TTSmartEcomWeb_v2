using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;

namespace TTSmartEcom.Infrastructure.MongoDb.Security;

public sealed class MongoUserIdentityReader(IMongoDatabaseProvider databaseProvider) : IUserIdentityReader
{
    private readonly IMongoCollection<BsonDocument> users =
        databaseProvider.Database.GetCollection<BsonDocument>("users");

    public async Task<UserIdentitySnapshot?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = ObjectId.TryParse(userId, out ObjectId objectId)
            ? Builders<BsonDocument>.Filter.Eq("_id", objectId)
            : Builders<BsonDocument>.Filter.Eq("_id", userId);

        BsonDocument? document = await users.Find(filter).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (!document.TryGetValue("_id", out BsonValue idValue))
        {
            return null;
        }

        string? id = idValue.ToString();
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }
        string role = ReadString(document, "role") ?? "customer";
        return new UserIdentitySnapshot(
            id,
            ReadString(document, "email"),
            ReadString(document, "phone") ?? string.Empty,
            ReadString(document, "name"),
            role,
            ReadStringArray(document, "functions"),
            ReadStringArray(document, "permissions"),
            ReadDate(document, "passwordChangedAt"),
            ReadStringArray(document, "station"));
    }

    private static string? ReadString(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out BsonValue value) || value.IsBsonNull)
        {
            return null;
        }

        return value.IsString ? value.AsString : value.ToString();
    }

    private static string[] ReadStringArray(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out BsonValue value) || !value.IsBsonArray)
        {
            return [];
        }

        return value.AsBsonArray
            .Where(item => item.IsString)
            .Select(item => item.AsString)
            .ToArray();
    }

    private static DateTimeOffset? ReadDate(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out BsonValue value) || !value.IsValidDateTime)
        {
            return null;
        }

        return new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero);
    }
}
