using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class ZaloConfigDocument : LegacyMongoDocument
{
    public const string CollectionName = "zaloconfigs";

    [BsonElement("appId")]
    public string? AppId { get; set; } = string.Empty;

    [BsonElement("secretKey")]
    public string? SecretKey { get; set; } = string.Empty;

    [BsonElement("oaId")]
    public string? OaId { get; set; } = string.Empty;

    [BsonElement("recipientUserId")]
    public string? RecipientUserId { get; set; } = string.Empty;

    [BsonElement("accessToken")]
    public string? AccessToken { get; set; } = string.Empty;

    [BsonElement("refreshToken")]
    public string? RefreshToken { get; set; } = string.Empty;

    [BsonElement("expiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ExpiresAt { get; set; }

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}
