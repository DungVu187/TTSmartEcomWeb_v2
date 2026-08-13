using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Users;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;

namespace TTSmartEcom.Infrastructure.MongoDb.Security;

public sealed class MongoUserRepository(IMongoDatabaseProvider databaseProvider) : IUserRepository
{
    private readonly IMongoCollection<BsonDocument> users =
        databaseProvider.Database.GetCollection<BsonDocument>("users");

    public async Task<UserRecord?> FindByLoginAsync(string identifier, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("phone", identifier),
            Builders<BsonDocument>.Filter.Eq("email", identifier.ToLowerInvariant()));

        BsonDocument? document = await users.Find(filter).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Map(document);
    }

    public async Task<PasswordRecoveryUser?> FindForPasswordRecoveryAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("phone", identifier),
            Builders<BsonDocument>.Filter.Eq("email", identifier.ToLowerInvariant()));
        BsonDocument? document = await users.Find(filter).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (document is null || !document.TryGetValue("_id", out BsonValue idValue))
        {
            return null;
        }

        string id = idValue.ToString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(id)
            ? null
            : new PasswordRecoveryUser(
                id,
                ReadString(document, "phone") ?? string.Empty,
                ReadString(document, "email"),
                ReadString(document, "name"));
    }

    public async Task<bool> StorePasswordResetOtpAsync(
        string userId,
        string otp,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("resetOtp", otp)
            .Set("resetOtpExpires", expiresAt.UtcDateTime);
        UpdateResult result = await users.UpdateOneAsync(
            BuildIdFilter(userId),
            update,
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    public async Task<bool> ClearPasswordResetOtpAsync(
        string userId,
        string expectedOtp,
        CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            BuildIdFilter(userId),
            Builders<BsonDocument>.Filter.Eq("resetOtp", expectedOtp));
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Unset("resetOtp")
            .Unset("resetOtpExpires");
        UpdateResult result = await users.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    public async Task<bool> ResetPasswordWithOtpAsync(
        string userId,
        string expectedOtp,
        DateTimeOffset now,
        string passwordHash,
        string replacementLoginToken,
        DateTimeOffset passwordChangedAt,
        CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            BuildIdFilter(userId),
            Builders<BsonDocument>.Filter.Eq("resetOtp", expectedOtp),
            Builders<BsonDocument>.Filter.Gte("resetOtpExpires", now.UtcDateTime));
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("password", passwordHash)
            .Set("logInString", replacementLoginToken)
            .Set("passwordChangedAt", passwordChangedAt.UtcDateTime)
            .Unset("resetOtp")
            .Unset("resetOtpExpires");
        UpdateResult result = await users.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    public async Task<UserRecord?> ConsumeAutologinTokenAsync(
        string token,
        string replacementToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(replacementToken))
        {
            return null;
        }

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("logInString", token);
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update.Set("logInString", replacementToken);
        BsonDocument? document = await users.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<BsonDocument>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = false,
            },
            cancellationToken);
        return document is null ? null : Map(document);
    }

    public async Task<UserIdentitySnapshot?> FindIdentityAsync(string userId, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = ObjectId.TryParse(userId, out ObjectId objectId)
            ? Builders<BsonDocument>.Filter.Eq("_id", objectId)
            : Builders<BsonDocument>.Filter.Eq("_id", userId);
        BsonDocument? document = await users.Find(filter).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return null;
        }

        UserRecord mapped = Map(document);
        return new UserIdentitySnapshot(
            mapped.Id,
            mapped.Email,
            mapped.Phone,
            mapped.Name,
            mapped.Role,
            mapped.Functions,
            mapped.Permissions,
            mapped.PasswordChangedAt);
    }

    private static UserRecord Map(BsonDocument document)
    {
        if (!document.TryGetValue("_id", out BsonValue idValue))
        {
            throw new InvalidOperationException("User document is missing _id.");
        }

        string id = idValue.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("User document has an invalid _id.");
        }
        return new UserRecord(
            id,
            ReadString(document, "phone") ?? string.Empty,
            ReadString(document, "email"),
            ReadString(document, "name"),
            ReadString(document, "password") ?? string.Empty,
            ReadString(document, "role") ?? "customer",
            ReadArray(document, "functions"),
            ReadArray(document, "permissions"),
            ReadDate(document, "passwordChangedAt"));
    }

    private static string? ReadString(BsonDocument document, string name) =>
        document.TryGetValue(name, out BsonValue value) && !value.IsBsonNull ? value.ToString() : null;

    private static string[] ReadArray(BsonDocument document, string name) =>
        document.TryGetValue(name, out BsonValue value) && value.IsBsonArray
            ? value.AsBsonArray.Where(item => item.IsString).Select(item => item.AsString).ToArray()
            : [];

    private static DateTimeOffset? ReadDate(BsonDocument document, string name) =>
        document.TryGetValue(name, out BsonValue value) && value.IsValidDateTime
            ? new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero)
            : null;

    private static FilterDefinition<BsonDocument> BuildIdFilter(string id)
    {
        FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
        return ObjectId.TryParse(id, out ObjectId objectId)
            ? builder.Or(builder.Eq("_id", objectId), builder.Eq("_id", id))
            : builder.Eq("_id", id);
    }
}
