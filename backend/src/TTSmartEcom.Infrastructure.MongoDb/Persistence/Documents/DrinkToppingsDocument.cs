using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class DrinkToppingsDocument : LegacyMongoDocument
{
    public const string CollectionName = "drinktoppings";

    [BsonElement("toppingNames")]
    [BsonIgnoreIfNull]
    public string? ToppingNames { get; set; }

    [BsonElement("toppingPrice")]
    [BsonIgnoreIfNull]
    public double? ToppingPrice { get; set; }
}
