using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class EpOrderDocument : LegacyMongoDocument
{
    public const string CollectionName = "eporders";

    [BsonElement("orderName")]
    public string? OrderName { get; set; } = string.Empty;

    [BsonElement("note")]
    public string? Note { get; set; } = string.Empty;

    [BsonElement("userName")]
    [BsonIgnoreIfNull]
    public string? UserName { get; set; }

    [BsonElement("productList")]
    public List<EpOrderLineDocument>? ProductList { get; set; } = [];

    [BsonElement("images")]
    public List<string>? Images { get; set; } = [];

    [BsonElement("total")]
    public string? Total { get; set; } = "0";

    [BsonElement("status")]
    public bool? Status { get; set; } = false;

    [BsonElement("transactionDate")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? TransactionDate { get; set; }

    [BsonElement("completedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class EpOrderLineDocument : LegacyMongoSubdocument
{
    [BsonElement("status")]
    public bool? Status { get; set; } = false;

    [BsonElement("productId")]
    [BsonIgnoreIfNull]
    [BsonSerializer(typeof(LegacyStringOrObjectIdSerializer))]
    public string? ProductId { get; set; }

    [BsonElement("price")]
    [BsonIgnoreIfNull]
    public string? Price { get; set; }

    [BsonElement("importPriceSnapshot")]
    public string? ImportPriceSnapshot { get; set; } = string.Empty;

    [BsonElement("profitPercent")]
    [BsonIgnoreIfNull]
    public double? ProfitPercent { get; set; }

    [BsonElement("unit")]
    [BsonIgnoreIfNull]
    public string? Unit { get; set; }

    [BsonElement("quantity")]
    public double? Quantity { get; set; } = 0;

    [BsonElement("quantityEx")]
    public double? ExportedQuantity { get; set; } = 0;

    [BsonElement("stockAppliedQuantity")]
    [BsonIgnoreIfNull]
    public double? StockAppliedQuantity { get; set; }

    [BsonElement("stockUpdateSkipped")]
    public bool? StockUpdateSkipped { get; set; } = false;

    [BsonElement("note")]
    [BsonIgnoreIfNull]
    public string? Note { get; set; }

    [BsonElement("vat")]
    public string? Vat { get; set; } = string.Empty;
}
