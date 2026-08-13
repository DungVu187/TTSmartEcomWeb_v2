using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class StorageHistoryDocument : LegacyMongoDocument
{
    public const string CollectionName = "storagehistories";

    [BsonElement("productId")]
    [BsonIgnoreIfNull]
    public ObjectId? ProductId { get; set; }

    [BsonElement("productName")]
    [BsonIgnoreIfNull]
    public string? ProductName { get; set; }

    [BsonElement("quantity")]
    [BsonIgnoreIfNull]
    public double? Quantity { get; set; }

    [BsonElement("userName")]
    [BsonIgnoreIfNull]
    public string? UserName { get; set; }

    [BsonElement("orderId")]
    [BsonIgnoreIfNull]
    public string? OrderId { get; set; }

    [BsonElement("orderName")]
    [BsonIgnoreIfNull]
    public string? OrderName { get; set; }

    [BsonElement("note")]
    public string? Note { get; set; } = string.Empty;

    [BsonElement("isAIScan")]
    public bool? IsAiScan { get; set; } = false;

    [BsonElement("source")]
    [BsonIgnoreIfNull]
    public string? Source { get; set; }

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}
