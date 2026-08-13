using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class OrderDocument : LegacyMongoDocument
{
    public const string CollectionName = "orders";

    [BsonElement("orderCode")]
    [BsonIgnoreIfNull]
    public string? OrderCode { get; set; }

    [BsonElement("userPhone")]
    public string? UserPhone { get; set; } = string.Empty;

    [BsonElement("userName")]
    [BsonIgnoreIfNull]
    public string? UserName { get; set; }

    [BsonElement("cartItems")]
    public List<OrderCartItemDocument>? CartItems { get; set; } = [];

    [BsonElement("total")]
    public double? Total { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; } = "Processing";

    [BsonElement("payment")]
    public bool? Payment { get; set; } = false;

    [BsonElement("state")]
    public string? State { get; set; } = "Processing";

    [BsonElement("completedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("images")]
    public List<string>? Images { get; set; } = [];

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class OrderCartItemDocument : LegacyMongoSubdocument
{
    [BsonElement("productId")]
    [BsonIgnoreIfNull]
    public string? ProductId { get; set; }

    [BsonElement("variantIndex")]
    public double? VariantIndex { get; set; }

    [BsonElement("quantity")]
    public double? Quantity { get; set; }
}
