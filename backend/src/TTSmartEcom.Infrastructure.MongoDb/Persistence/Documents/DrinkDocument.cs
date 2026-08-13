using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class DrinkDocument : LegacyMongoDocument
{
    public const string CollectionName = "drinks";

    [BsonElement("drinkName")]
    [BsonIgnoreIfNull]
    public string? DrinkName { get; set; }

    [BsonElement("drinkPrice")]
    [BsonIgnoreIfNull]
    public double? DrinkPrice { get; set; }

    [BsonElement("drinkImg")]
    [BsonIgnoreIfNull]
    public string? DrinkImage { get; set; }

    [BsonElement("toppings")]
    public List<string>? Toppings { get; set; } = [];
}
