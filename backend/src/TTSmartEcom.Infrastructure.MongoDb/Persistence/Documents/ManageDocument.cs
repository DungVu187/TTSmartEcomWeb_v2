using MongoDB.Bson.Serialization.Attributes;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class ManageDocument : LegacyMongoDocument
{
    public const string CollectionName = "manages";

    [BsonElement("overViewImg")]
    public List<string>? OverviewImages { get; set; } = [];

    [BsonElement("partners")]
    public List<string>? Partners { get; set; } = [];

    [BsonElement("displayPartners")]
    public bool? DisplayPartners { get; set; } = true;

    [BsonElement("footerContent")]
    public ManageFooterContentDocument? FooterContent { get; set; } = new();

    [BsonElement("newProductUrl")]
    public string? NewProductUrl { get; set; } = string.Empty;

    [BsonElement("topPurchaseUrl")]
    public string? TopPurchaseUrl { get; set; } = string.Empty;

    [BsonElement("highestRatingUrl")]
    public string? HighestRatingUrl { get; set; } = string.Empty;

    [BsonElement("introduction")]
    public string? Introduction { get; set; } = string.Empty;

    [BsonElement("introductionTranslations")]
    public LocalizedTextDocument? IntroductionTranslations { get; set; } = new();

    [BsonElement("mainPolicy")]
    public string? MainPolicy { get; set; } = string.Empty;

    [BsonElement("policies")]
    public List<ManagePolicyDocument>? Policies { get; set; } = [];

    [BsonElement("homeCategoryConfig")]
    public ManageHomeCategoryConfigDocument? HomeCategoryConfig { get; set; } = new();

    [BsonElement("section1")]
    public ManageSection1Document? Section1 { get; set; } = new();

    [BsonElement("section2")]
    public ManageSectionDocument? Section2 { get; set; } = new();

    [BsonElement("section3")]
    public ManageSectionDocument? Section3 { get; set; } = new();

    [BsonElement("section4")]
    public ManageSectionDocument? Section4 { get; set; } = new();

    [BsonElement("section5")]
    public ManageSectionDocument? Section5 { get; set; } = new();

    [BsonElement("section6")]
    public ManageSectionDocument? Section6 { get; set; } = new();

    [BsonElement("section7")]
    public ManageSectionDocument? Section7 { get; set; } = new();

    [BsonElement("section8")]
    public ManageSectionDocument? Section8 { get; set; } = new();

    [BsonElement("section9")]
    public ManageSectionDocument? Section9 { get; set; } = new();

    [BsonElement("section10")]
    public ManageSectionDocument? Section10 { get; set; } = new();

    [BsonElement("section11")]
    public ManageSectionDocument? Section11 { get; set; } = new();

    [BsonElement("createdAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ManageFooterContentDocument : LegacyMongoValue
{
    [BsonElement("logo")]
    public string? Logo { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; } = string.Empty;

    [BsonElement("address")]
    public string? Address { get; set; } = string.Empty;

    [BsonElement("phone")]
    public string? Phone { get; set; } = string.Empty;

    [BsonElement("email")]
    public string? Email { get; set; } = string.Empty;
}

public sealed class LocalizedTextDocument : LegacyMongoValue
{
    [BsonElement("vi")]
    public string? Vietnamese { get; set; } = string.Empty;

    [BsonElement("zh")]
    public string? Chinese { get; set; } = string.Empty;

    [BsonElement("en")]
    public string? English { get; set; } = string.Empty;
}

public sealed class ManageHomeCategoryConfigDocument : LegacyMongoValue
{
    [BsonElement("configured")]
    public bool? Configured { get; set; } = false;

    [BsonElement("sidebarTitle")]
    public string? SidebarTitle { get; set; } = "Danh mục sản phẩm";

    [BsonElement("sidebarTitleTranslations")]
    public LocalizedTextDocument? SidebarTitleTranslations { get; set; } = new();

    [BsonElement("showSidebar")]
    public bool? ShowSidebar { get; set; } = true;

    [BsonElement("showQuickCategories")]
    public bool? ShowQuickCategories { get; set; } = true;

    [BsonElement("items")]
    public List<ManageHomeCategoryItemDocument>? Items { get; set; } = [];
}

public sealed class ManageHomeCategoryItemDocument : LegacyMongoValue
{
    [BsonElement("id")]
    public string? Id { get; set; } = string.Empty;

    [BsonElement("label")]
    public string? Label { get; set; } = string.Empty;

    [BsonElement("labelTranslations")]
    public LocalizedTextDocument? LabelTranslations { get; set; } = new();

    [BsonElement("type")]
    public string? Type { get; set; } = string.Empty;

    [BsonElement("link")]
    public string? Link { get; set; } = string.Empty;

    [BsonElement("icon")]
    public string? Icon { get; set; } = "ri-tb-box-multiple";

    [BsonElement("image")]
    public string? Image { get; set; } = string.Empty;

    [BsonElement("showSidebar")]
    public bool? ShowSidebar { get; set; } = true;

    [BsonElement("showQuick")]
    public bool? ShowQuick { get; set; } = true;
}

public sealed class ManageSectionDocument : LegacyMongoValue
{
    [BsonElement("name")]
    public string? Name { get; set; } = string.Empty;

    [BsonElement("nameTranslations")]
    public LocalizedTextDocument? NameTranslations { get; set; } = new();

    [BsonElement("productId")]
    public List<string>? ProductIds { get; set; } = [];

    [BsonElement("display")]
    public bool? Display { get; set; } = true;

    [BsonElement("image")]
    public string? Image { get; set; } = string.Empty;

    [BsonElement("link")]
    public string? Link { get; set; } = string.Empty;
}

public sealed class ManageSection1Document : LegacyMongoValue
{
    [BsonElement("name")]
    public string? Name { get; set; } = string.Empty;

    [BsonElement("nameTranslations")]
    public LocalizedTextDocument? NameTranslations { get; set; } = new();

    [BsonElement("productId")]
    public List<string>? ProductIds { get; set; } = [];

    [BsonElement("display")]
    public bool? Display { get; set; } = true;
}

public sealed class ManagePolicyDocument : LegacyMongoValue
{
    [BsonElement("key")]
    [BsonIgnoreIfNull]
    public string? Key { get; set; }

    [BsonElement("title")]
    [BsonIgnoreIfNull]
    public string? Title { get; set; }

    [BsonElement("summary")]
    public string? Summary { get; set; } = string.Empty;

    [BsonElement("sections")]
    public List<ManagePolicySectionDocument>? Sections { get; set; } = [];

    [BsonElement("translations")]
    public ManagePolicyTranslationsDocument? Translations { get; set; } = new();

    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ManagePolicyTranslationsDocument : LegacyMongoValue
{
    [BsonElement("vi")]
    [BsonIgnoreIfNull]
    public ManagePolicyContentDocument? Vietnamese { get; set; }

    [BsonElement("zh")]
    [BsonIgnoreIfNull]
    public ManagePolicyContentDocument? Chinese { get; set; }

    [BsonElement("en")]
    [BsonIgnoreIfNull]
    public ManagePolicyContentDocument? English { get; set; }
}

public sealed class ManagePolicyContentDocument : LegacyMongoValue
{
    [BsonElement("title")]
    [BsonIgnoreIfNull]
    public string? Title { get; set; }

    [BsonElement("summary")]
    public string? Summary { get; set; } = string.Empty;

    [BsonElement("sections")]
    public List<ManagePolicySectionDocument>? Sections { get; set; } = [];
}

public sealed class ManagePolicySectionDocument : LegacyMongoValue
{
    [BsonElement("title")]
    [BsonIgnoreIfNull]
    public string? Title { get; set; }

    [BsonElement("content")]
    [BsonIgnoreIfNull]
    public string? Content { get; set; }
}
