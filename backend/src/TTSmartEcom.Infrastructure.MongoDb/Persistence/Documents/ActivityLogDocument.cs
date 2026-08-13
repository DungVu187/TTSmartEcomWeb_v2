using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class ActivityLogDocument : LegacyMongoDocument
{
    public const string CollectionName = "activitylogs";

    [BsonElement("userName")]
    [BsonIgnoreIfNull]
    public string? UserName { get; set; }

    [BsonElement("action")]
    [BsonIgnoreIfNull]
    public string? Action { get; set; }

    [BsonElement("productId")]
    [BsonIgnoreIfNull]
    public ObjectId? ProductId { get; set; }

    [BsonElement("productName")]
    [BsonIgnoreIfNull]
    public string? ProductName { get; set; }

    [BsonElement("details")]
    public List<ActivityLogDetailDocument>? Details { get; set; } = [];

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }

    public sealed class ActivityLogDetailDocument : LegacyMongoSubdocument
    {
        [BsonElement("field")]
        [BsonIgnoreIfNull]
        public string? Field { get; set; }

        [BsonElement("oldValue")]
        public string? OldValue { get; set; } = string.Empty;

        [BsonElement("newValue")]
        public string? NewValue { get; set; } = string.Empty;
    }
}
