using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class UserDocument : LegacyMongoDocument
{
    public const string CollectionName = "users";

    [BsonElement("email")]
    [BsonIgnoreIfNull]
    public string? Email { get; set; }

    [BsonElement("phone")]
    [BsonIgnoreIfNull]
    public string? Phone { get; set; }

    [BsonElement("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonElement("cart")]
    public List<UserCartItemDocument>? Cart { get; set; } = [];

    [BsonElement("password")]
    [BsonIgnoreIfNull]
    public string? Password { get; set; }

    [BsonElement("passwordChangedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? PasswordChangedAt { get; set; }

    [BsonElement("role")]
    public string? Role { get; set; } = "customer";

    [BsonElement("functions")]
    public List<string>? Functions { get; set; } = [];

    [BsonElement("permissions")]
    public List<string>? Permissions { get; set; } = [];

    [BsonElement("orderTemplate")]
    public List<UserOrderTemplateDocument>? OrderTemplates { get; set; } = [];

    [BsonElement("station")]
    public List<string>? Stations { get; set; } = [];

    [BsonElement("addresses")]
    public List<UserAddressDocument>? Addresses { get; set; } = [];

    [BsonElement("logInString")]
    [BsonIgnoreIfNull]
    public string? LoginString { get; set; }

    [BsonElement("resetOtp")]
    [BsonIgnoreIfNull]
    public string? ResetOtp { get; set; }

    [BsonElement("resetOtpExpires")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ResetOtpExpires { get; set; }
}

public sealed class UserCartItemDocument : LegacyMongoSubdocument
{
    [BsonElement("productId")]
    [BsonIgnoreIfNull]
    public string? ProductId { get; set; }

    [BsonElement("quantity")]
    public double? Quantity { get; set; } = 1;

    [BsonElement("variantIndex")]
    [BsonIgnoreIfNull]
    public double? VariantIndex { get; set; }

    [BsonElement("status")]
    public bool? Status { get; set; } = true;
}

public sealed class UserOrderTemplateDocument : LegacyMongoSubdocument
{
    [BsonElement("displayName")]
    [BsonIgnoreIfNull]
    public string? DisplayName { get; set; }

    [BsonElement("note")]
    public string? Note { get; set; } = string.Empty;

    [BsonElement("products")]
    public List<UserTemplateProductDocument>? Products { get; set; } = [];
}

public sealed class UserTemplateProductDocument : LegacyMongoSubdocument
{
    [BsonElement("productId")]
    [BsonIgnoreIfNull]
    public string? ProductId { get; set; }

    [BsonElement("quantity")]
    public double? Quantity { get; set; } = 1;
}

public sealed class UserAddressDocument : LegacyMongoSubdocument
{
    [BsonElement("label")]
    public string? Label { get; set; } = "Công trình";

    [BsonElement("receiverName")]
    [BsonIgnoreIfNull]
    public string? ReceiverName { get; set; }

    [BsonElement("receiverPhone")]
    [BsonIgnoreIfNull]
    public string? ReceiverPhone { get; set; }

    [BsonElement("addressDetail")]
    [BsonIgnoreIfNull]
    public string? AddressDetail { get; set; }

    [BsonElement("isDefault")]
    public bool? IsDefault { get; set; } = false;
}
