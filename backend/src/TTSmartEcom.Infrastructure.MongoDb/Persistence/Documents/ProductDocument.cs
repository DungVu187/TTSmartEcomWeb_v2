using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class ProductDocument : LegacyMongoDocument
{
    public const string CollectionName = "products";

    [BsonElement("type")]
    [BsonIgnoreIfNull]
    public string? Type { get; set; }

    [BsonElement("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonElement("nameUnsigned")]
    [BsonIgnoreIfNull]
    public string? NameUnsigned { get; set; }

    [BsonElement("display")]
    public bool? Display { get; set; } = true;

    [BsonElement("code")]
    [BsonIgnoreIfNull]
    public string? Code { get; set; }

    [BsonElement("vat")]
    public string? Vat { get; set; } = string.Empty;

    [BsonElement("adjusted")]
    public bool? Adjusted { get; set; } = true;

    [BsonElement("brand")]
    [BsonIgnoreIfNull]
    public string? Brand { get; set; }

    [BsonElement("section")]
    [BsonIgnoreIfNull]
    public string? Section { get; set; }

    [BsonElement("value")]
    [BsonIgnoreIfNull]
    public string? Value { get; set; }

    [BsonElement("variant")]
    public List<ProductVariantDocument>? Variants { get; set; } = [];

    [BsonElement("infoDoc")]
    [BsonIgnoreIfNull]
    public ProductInfoDocument? InfoDoc { get; set; }

    [BsonElement("documents")]
    public List<ProductDocumentLink>? Documents { get; set; } = [];

    [BsonElement("purchaseCount")]
    public long? PurchaseCount { get; set; } = 0;

    [BsonElement("reviews")]
    public List<ProductReviewDocument>? Reviews { get; set; } = [];

    [BsonElement("totalRating")]
    public double? TotalRating { get; set; } = 0;

    [BsonElement("reviewCount")]
    public long? ReviewCount { get; set; } = 0;

    [BsonElement("averageReviews")]
    public double? AverageReviews { get; set; } = 0;

    [BsonElement("warranty")]
    [BsonIgnoreIfNull]
    public string? Warranty { get; set; }

    [BsonElement("solution")]
    public string? Solution { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; } = string.Empty;

    [BsonElement("features")]
    public string? Features { get; set; } = string.Empty;

    [BsonElement("operatingMethod")]
    public string? OperatingMethod { get; set; } = string.Empty;

    [BsonElement("advantages")]
    public string? Advantages { get; set; } = string.Empty;

    [BsonElement("specifications")]
    public string? Specifications { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ProductVariantDocument : LegacyMongoSubdocument
{
    [BsonElement("price")]
    public string? Price { get; set; } = string.Empty;

    [BsonElement("importPrice")]
    public string? ImportPrice { get; set; } = string.Empty;

    [BsonElement("earn")]
    public double? Earn { get; set; } = 25;

    [BsonElement("imgUrl")]
    public string? ImageUrl { get; set; } = string.Empty;

    [BsonElement("color")]
    public string? Color { get; set; } = string.Empty;

    [BsonElement("shape")]
    public string? Shape { get; set; } = string.Empty;

    [BsonElement("buttonCount")]
    public string? ButtonCount { get; set; } = string.Empty;

    [BsonElement("frame")]
    public string? Frame { get; set; } = string.Empty;

    [BsonElement("quantityForSale")]
    public double? QuantityForSale { get; set; } = 0;

    [BsonElement("quantityInStorage")]
    public double? QuantityInStorage { get; set; } = 0;

    [BsonElement("note")]
    public string? Note { get; set; } = string.Empty;
}

public sealed class ProductInfoDocument : LegacyMongoValue
{
    [BsonElement("manual")]
    public string? Manual { get; set; } = string.Empty;

    [BsonElement("dataSheet")]
    public string? DataSheet { get; set; } = string.Empty;

    [BsonElement("catalog")]
    public string? Catalog { get; set; } = string.Empty;

    [BsonElement("others")]
    public string? Others { get; set; } = string.Empty;
}

public sealed class ProductDocumentLink : LegacyMongoSubdocument
{
    [BsonElement("label")]
    public string? Label { get; set; } = string.Empty;

    [BsonElement("url")]
    public string? Url { get; set; } = string.Empty;

    [BsonElement("sourceType")]
    public string? SourceType { get; set; } = string.Empty;
}

public sealed class ProductReviewDocument : LegacyMongoSubdocument
{
    [BsonElement("email")]
    [BsonIgnoreIfNull]
    public string? Email { get; set; }

    [BsonElement("comment")]
    public string? Comment { get; set; } = string.Empty;

    [BsonElement("rating")]
    [BsonIgnoreIfNull]
    public double? Rating { get; set; }

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }
}
