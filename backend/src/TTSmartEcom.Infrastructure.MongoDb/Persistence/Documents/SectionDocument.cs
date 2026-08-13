using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class SectionDocument : LegacyMongoDocument
{
    public const string CollectionName = "sections";

    [BsonElement("Section")]
    public List<SectionItemDocument>? Sections { get; set; } = [];

    public sealed class SectionItemDocument : LegacyMongoSubdocument
    {
        [BsonElement("name")]
        [BsonIgnoreIfNull]
        public string? Name { get; set; }

        [BsonElement("value")]
        public List<string>? Value { get; set; } = [];

        [BsonElement("imgUrl")]
        [BsonIgnoreIfNull]
        public string? ImageUrl { get; set; }
    }
}
