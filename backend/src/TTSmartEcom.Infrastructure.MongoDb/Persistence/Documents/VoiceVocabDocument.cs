using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class VoiceVocabDocument : LegacyMongoDocument
{
    public const string CollectionName = "voicevocabs";

    [BsonElement("stopwords")]
    public List<string>? Stopwords { get; set; } = [];

    [BsonElement("brands")]
    public List<string>? Brands { get; set; } = [];

    [BsonElement("types")]
    public List<string>? Types { get; set; } = [];

    [BsonElement("brandAliases")]
    public List<VoiceBrandAliasDocument>? BrandAliases { get; set; } = [];

    [BsonElement("typeAliases")]
    public List<VoiceTypeAliasDocument>? TypeAliases { get; set; } = [];

    [BsonElement("intentAliases")]
    public List<VoiceIntentAliasDocument>? IntentAliases { get; set; } = [];

    [BsonElement("codeMap")]
    public List<VoiceCodeMapDocument>? CodeMap { get; set; } = [];

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class VoiceBrandAliasDocument : LegacyMongoValue
{
    [BsonElement("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonElement("aliases")]
    public List<string>? Aliases { get; set; } = [];
}

public sealed class VoiceTypeAliasDocument : LegacyMongoValue
{
    [BsonElement("type")]
    [BsonIgnoreIfNull]
    public string? Type { get; set; }

    [BsonElement("keyword")]
    public string? Keyword { get; set; } = string.Empty;

    [BsonElement("aliases")]
    public List<string>? Aliases { get; set; } = [];
}

public sealed class VoiceIntentAliasDocument : LegacyMongoValue
{
    [BsonElement("intent")]
    [BsonIgnoreIfNull]
    public string? Intent { get; set; }

    [BsonElement("label")]
    public string? Label { get; set; } = string.Empty;

    [BsonElement("aliases")]
    public List<string>? Aliases { get; set; } = [];
}

public sealed class VoiceCodeMapDocument : LegacyMongoValue
{
    [BsonElement("code")]
    [BsonIgnoreIfNull]
    public string? Code { get; set; }

    [BsonElement("keyword")]
    public string? Keyword { get; set; } = string.Empty;

    [BsonElement("brand")]
    public string? Brand { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("patterns")]
    public List<string>? Patterns { get; set; } = [];

    [BsonElement("compact")]
    public string? Compact { get; set; } = string.Empty;
}
