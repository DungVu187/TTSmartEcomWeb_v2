using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class BrandDocument : LegacyMongoDocument
{
    public const string CollectionName = "brands";

    [BsonElement("Brand")]
    [BsonIgnoreIfNull]
    public string? Brand { get; set; }
}
