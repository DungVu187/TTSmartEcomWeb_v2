using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Domain.Catalog;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Catalog;

public sealed class MongoCatalogRepository(IMongoDatabaseProvider databaseProvider) : ICatalogRepository
{
    private readonly IMongoCollection<BsonDocument> brands =
        databaseProvider.Database.GetCollection<BsonDocument>(BrandDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> sections =
        databaseProvider.Database.GetCollection<BsonDocument>(SectionDocument.CollectionName);
    private readonly IMongoCollection<BsonDocument> manages =
        databaseProvider.Database.GetCollection<BsonDocument>(ManageDocument.CollectionName);

    public async Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(CancellationToken cancellationToken)
    {
        List<BsonDocument> documents = await brands.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("Brand"))
            .ToListAsync(cancellationToken);
        return documents.Select(document => new BrandRecord(ReadId(document), ReadString(document, "Brand"))).ToArray();
    }

    public async Task<IReadOnlyList<string>> ListSectionNamesAsync(CancellationToken cancellationToken)
    {
        BsonDocument? document = await sections.Find(FilterDefinition<BsonDocument>.Empty)
            .Limit(1).FirstOrDefaultAsync(cancellationToken);
        return ReadSectionItems(document).Select(item => ReadString(item, "name") ?? string.Empty)
            .Where(name => name.Length > 0).ToArray();
    }

    public async Task<SectionDocumentRecord?> GetSectionDocumentAsync(CancellationToken cancellationToken)
    {
        BsonDocument? document = await sections.Find(FilterDefinition<BsonDocument>.Empty)
            .Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : MapSectionDocument(document);
    }

    public async Task<IReadOnlyList<string>?> GetSectionValuesAsync(string sectionName, CancellationToken cancellationToken)
    {
        BsonDocument? document = await sections.Find(FilterDefinition<BsonDocument>.Empty)
            .Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (document is null) return null;
        BsonDocument? section = ReadSectionItems(document)
            .FirstOrDefault(item => string.Equals(ReadString(item, "name"), sectionName, StringComparison.Ordinal));
        return section is null ? null : ReadArrayStrings(section, "value");
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetSectionImagesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken)
    {
        BsonDocument? document = await sections.Find(FilterDefinition<BsonDocument>.Empty)
            .Limit(1).FirstOrDefaultAsync(cancellationToken);
        Dictionary<string, string?> result = new(StringComparer.Ordinal);
        foreach (string name in names)
        {
            BsonDocument? section = ReadSectionItems(document)
                .FirstOrDefault(item => string.Equals(ReadString(item, "name"), name, StringComparison.Ordinal));
            result[name] = section is null ? null : NormalizeAssetUrl(ReadString(section, "imgUrl"));
        }

        return result;
    }

    public async Task<ManageRecord?> GetManageAsync(CancellationToken cancellationToken)
    {
        BsonDocument? document = await manages.Find(FilterDefinition<BsonDocument>.Empty)
            .Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : MapManage(document);
    }

    public async Task<IReadOnlyList<ManagePolicyRecord>> GetPoliciesAsync(CancellationToken cancellationToken)
    {
        BsonDocument? document = await manages.Find(FilterDefinition<BsonDocument>.Empty)
            .Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? [] : MapPolicies(document);
    }

    private static SectionDocumentRecord MapSectionDocument(BsonDocument document) =>
        new(ReadId(document), ReadSectionItems(document).Select(MapSectionItem).ToArray());

    private static SectionItemRecord MapSectionItem(BsonDocument document) =>
        new(ReadId(document), ReadString(document, "name"), ReadArrayStrings(document, "value"),
            NormalizeAssetUrl(ReadString(document, "imgUrl")));

    private static ManageRecord MapManage(BsonDocument document)
    {
        Dictionary<string, ManageSectionRecord> sectionMap = new(StringComparer.Ordinal);
        for (int index = 1; index <= 11; index++)
        {
            string key = $"section{index}";
            if (document.TryGetValue(key, out BsonValue value) && value.IsBsonDocument)
            {
                sectionMap[key] = MapManageSection(value.AsBsonDocument);
            }
        }

        return new ManageRecord(
            ReadId(document), ReadArrayStrings(document, "overViewImg").Select(NormalizeAssetUrlOrSelf).ToArray(),
            ReadArrayStrings(document, "partners").Select(NormalizeAssetUrlOrSelf).ToArray(),
            ReadBool(document, "displayPartners"), MapFooter(document), ReadString(document, "newProductUrl"),
            ReadString(document, "topPurchaseUrl"), ReadString(document, "highestRatingUrl"),
            ReadString(document, "introduction"), MapLocalized(document, "introductionTranslations"),
            ReadString(document, "mainPolicy"), MapPolicies(document), MapHomeCategories(document), sectionMap,
            ReadDate(document, "createdAt"), ReadDate(document, "updatedAt"));
    }

    private static ManageFooterRecord? MapFooter(BsonDocument document)
    {
        if (!document.TryGetValue("footerContent", out BsonValue value) || !value.IsBsonDocument) return null;
        BsonDocument footer = value.AsBsonDocument;
        return new ManageFooterRecord(ReadString(footer, "logo"), ReadString(footer, "description"),
            ReadString(footer, "address"), ReadString(footer, "phone"), ReadString(footer, "email"));
    }

    private static ManageHomeCategoryRecord? MapHomeCategories(BsonDocument document)
    {
        if (!document.TryGetValue("homeCategoryConfig", out BsonValue value) || !value.IsBsonDocument) return null;
        BsonDocument home = value.AsBsonDocument;
        List<ManageCategoryItemRecord> items = ReadDocuments(home, "items").Select(item => new ManageCategoryItemRecord(
            ReadString(item, "id"), ReadString(item, "label"), MapLocalized(item, "labelTranslations"),
            ReadString(item, "type"), ReadString(item, "link"), ReadString(item, "icon"), ReadString(item, "image"),
            ReadBool(item, "showSidebar"), ReadBool(item, "showQuick"))).ToList();
        return new ManageHomeCategoryRecord(ReadBool(home, "configured"), ReadString(home, "sidebarTitle"),
            MapLocalized(home, "sidebarTitleTranslations"), ReadBool(home, "showSidebar"),
            ReadBool(home, "showQuickCategories"), items);
    }

    private static ManageSectionRecord MapManageSection(BsonDocument document) =>
        new(ReadString(document, "name"), MapLocalized(document, "nameTranslations"),
            ReadArrayStrings(document, "productId"), ReadBool(document, "display"),
            NormalizeAssetUrl(ReadString(document, "image")), ReadString(document, "link"));

    private static ManagePolicyRecord[] MapPolicies(BsonDocument document)
    {
        if (!document.TryGetValue("policies", out BsonValue value) || !value.IsBsonArray) return [];
        return value.AsBsonArray.Where(item => item.IsBsonDocument).Select(item =>
        {
            BsonDocument policy = item.AsBsonDocument;
            return new ManagePolicyRecord(ReadString(policy, "key"), ReadString(policy, "title"),
                ReadString(policy, "summary"), ReadDocuments(policy, "sections")
                    .Select(section => new ManagePolicySectionRecord(ReadString(section, "title"), ReadString(section, "content")))
                    .ToArray(), ReadDate(policy, "updatedAt"));
        }).ToArray();
    }

    private static LocalizedTextRecord? MapLocalized(BsonDocument document, string field)
    {
        if (!document.TryGetValue(field, out BsonValue value) || !value.IsBsonDocument) return null;
        BsonDocument localized = value.AsBsonDocument;
        return new LocalizedTextRecord(ReadString(localized, "vi"), ReadString(localized, "zh"), ReadString(localized, "en"));
    }

    private static IEnumerable<BsonDocument> ReadSectionItems(BsonDocument? document)
    {
        if (document is null || !document.TryGetValue("Section", out BsonValue value) || !value.IsBsonArray) return [];
        return value.AsBsonArray.Where(item => item.IsBsonDocument).Select(item => item.AsBsonDocument);
    }

    private static List<BsonDocument> ReadDocuments(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsBsonArray
            ? value.AsBsonArray.Where(item => item.IsBsonDocument).Select(item => item.AsBsonDocument).ToList()
            : [];

    private static string[] ReadArrayStrings(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsBsonArray
            ? value.AsBsonArray.Where(item => item.IsString).Select(item => item.AsString).ToArray()
            : [];

    private static string ReadId(BsonDocument document)
    {
        if (!document.TryGetValue("_id", out BsonValue value) || value.IsBsonNull) return string.Empty;
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string? ReadString(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull
            ? value.IsString ? value.AsString : value.ToString()
            : null;

    private static bool? ReadBool(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsBoolean ? value.AsBoolean : null;

    private static DateTimeOffset? ReadDate(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsValidDateTime
            ? new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero)
            : null;

    private static string? NormalizeAssetUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        string[] markers = ["/images/", "/station/", "/section-images/"];
        foreach (string marker in markers)
        {
            int index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return value[index..];
        }

        return value;
    }

    private static string NormalizeAssetUrlOrSelf(string value) => NormalizeAssetUrl(value) ?? string.Empty;
}
