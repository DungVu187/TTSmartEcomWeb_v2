using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class DrinkBillDocument : LegacyMongoDocument
{
    public const string CollectionName = "drinkbills";

    [BsonElement("detail")]
    public List<DrinkBillDetailDocument>? Detail { get; set; } = [];

    [BsonElement("billTotal")]
    [BsonIgnoreIfNull]
    public double? BillTotal { get; set; }

    [BsonElement("billStatus")]
    [BsonIgnoreIfNull]
    public bool? BillStatus { get; set; }

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }

    public sealed class DrinkBillDetailDocument : LegacyMongoSubdocument
    {
        [BsonElement("staff")]
        [BsonIgnoreIfNull]
        public string? Staff { get; set; }

        [BsonElement("drinkImg")]
        [BsonIgnoreIfNull]
        public string? DrinkImage { get; set; }

        [BsonElement("drink")]
        [BsonIgnoreIfNull]
        public string? Drink { get; set; }

        [BsonElement("toppings")]
        [BsonIgnoreIfNull]
        public string? Toppings { get; set; }

        [BsonElement("drinkPrice")]
        [BsonIgnoreIfNull]
        public double? DrinkPrice { get; set; }

        [BsonElement("status")]
        [BsonIgnoreIfNull]
        public bool? Status { get; set; }
    }
}
