using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class CounterDocument : LegacyMongoDocument
{
    public const string CollectionName = "counters";

    [BsonElement("id")]
    [BsonIgnoreIfNull]
    public string? CounterId { get; set; }

    [BsonElement("seq")]
    public long? Sequence { get; set; } = 0;
}
