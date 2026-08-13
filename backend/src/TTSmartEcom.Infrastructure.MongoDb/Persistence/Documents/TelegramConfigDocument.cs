using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class TelegramConfigDocument : LegacyMongoDocument
{
    public const string CollectionName = "telegramconfigs";

    [BsonElement("enabled")]
    public bool? Enabled { get; set; } = false;

    [BsonElement("recipients")]
    public List<TelegramRecipientDocument>? Recipients { get; set; } = [];

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class TelegramRecipientDocument : LegacyMongoSubdocument
{
    [BsonElement("label")]
    public string? Label { get; set; } = string.Empty;

    [BsonElement("chatId")]
    [BsonIgnoreIfNull]
    public string? ChatId { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; } = "personal";

    [BsonElement("enabled")]
    public bool? Enabled { get; set; } = true;

    [BsonElement("notifyTypes")]
    public List<string>? NotifyTypes { get; set; } = ["new_order"];
}
