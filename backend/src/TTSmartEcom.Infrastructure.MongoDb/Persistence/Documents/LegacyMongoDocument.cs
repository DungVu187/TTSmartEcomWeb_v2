using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public abstract class LegacyMongoEntity
{
    [BsonExtraElements]
    public BsonDocument? ExtraElements { get; set; }
}

public abstract class LegacyMongoDocument : LegacyMongoEntity
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("__v")]
    [BsonIgnoreIfNull]
    public int? Version { get; set; }
}

public abstract class LegacyMongoSubdocument : LegacyMongoEntity
{
    [BsonElement("_id")]
    [BsonIgnoreIfNull]
    public ObjectId? Id { get; set; }
}

/// <summary>
/// Embedded object used by legacy schemas that explicitly disable Mongoose's
/// automatic subdocument _id (for example localized text and policy content).
/// </summary>
public abstract class LegacyMongoValue : LegacyMongoEntity
{
}
