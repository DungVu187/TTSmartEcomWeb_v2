using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class ChipDocument : LegacyMongoDocument
{
    public const string CollectionName = "chips";

    [BsonElement("Color")]
    public List<string>? Color { get; set; } = [];

    [BsonElement("Shapes")]
    public List<string>? Shapes { get; set; } = [];

    [BsonElement("Frames")]
    public List<string>? Frames { get; set; } = [];

    [BsonElement("ButtonCount")]
    public List<string>? ButtonCount { get; set; } = [];
}
