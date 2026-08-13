using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class DrinkOweListDocument : LegacyMongoDocument
{
    public const string CollectionName = "drinkowelists";

    [BsonElement("staffID")]
    [BsonIgnoreIfNull]
    public string? StaffId { get; set; }

    [BsonElement("bank")]
    [BsonIgnoreIfNull]
    public double? Bank { get; set; }
}
