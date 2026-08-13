using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class ProductTypeDocument : LegacyMongoDocument
{
    public const string CollectionName = "types";

    [BsonElement("Type")]
    [BsonIgnoreIfNull]
    public string? Type { get; set; }

    [BsonElement("icon")]
    [BsonIgnoreIfNull]
    public string? Icon { get; set; }

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}
