using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class StationDocument : LegacyMongoDocument
{
    public const string CollectionName = "stations";

    [BsonElement("stationName")]
    [BsonIgnoreIfNull]
    public string? StationName { get; set; }

    [BsonElement("imgUrl")]
    [BsonIgnoreIfNull]
    public string? ImageUrl { get; set; }

    [BsonElement("stationCode")]
    [BsonIgnoreIfNull]
    public string? StationCode { get; set; }

    [BsonElement("allowPublicSignup")]
    public bool? AllowPublicSignup { get; set; } = true;

    [BsonElement("location")]
    [BsonIgnoreIfNull]
    public string? Location { get; set; }

    [BsonElement("productId")]
    public List<string>? ProductIds { get; set; } = [];
}
