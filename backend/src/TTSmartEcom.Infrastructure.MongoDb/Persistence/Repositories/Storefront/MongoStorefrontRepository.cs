using MongoDB.Bson;
using MongoDB.Driver;
using TTSmartEcom.Application.Storefront;
using TTSmartEcom.Domain.Storefront;
using TTSmartEcom.Infrastructure.MongoDb.Configuration;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Storefront;

public sealed class MongoStorefrontRepository(IMongoDatabaseProvider databaseProvider) : IStorefrontRepository
{
    private static readonly string[] SingleImageFields = ["newProductUrl", "topPurchaseUrl", "highestRatingUrl"];
    private readonly IMongoCollection<BsonDocument> manages = databaseProvider.Database.GetCollection<BsonDocument>(ManageDocument.CollectionName);

    public async Task<StorefrontContent?> GetAsync(CancellationToken cancellationToken)
    {
        BsonDocument? document = await manages.Find(Builders<BsonDocument>.Filter.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Map(document);
    }

    public async Task<StorefrontContent> UpsertAsync(StorefrontPatch patch, CancellationToken cancellationToken)
    {
        BsonDocument document = await manages.Find(Builders<BsonDocument>.Filter.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken)
            ?? new BsonDocument { ["_id"] = ObjectId.GenerateNewId() };
        ApplyPatch(document, patch);
        document["updatedAt"] = DateTime.UtcNow;
        if (!document.Contains("createdAt")) document["createdAt"] = DateTime.UtcNow;
        if (document.Contains("_id") && document["_id"].IsObjectId && document["_id"].AsObjectId != default && document.Contains("__new"))
            document.Remove("__new");
        await manages.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        return Map(document);
    }

    public async Task<StorefrontContent> UpdateSectionAsync(string section, StorefrontSectionPatch patch, CancellationToken cancellationToken)
    {
        BsonDocument document = await GetOrCreateAsync(cancellationToken);
        string field = section.Trim().ToLowerInvariant() switch
        {
            "section1" or "section2" or "section3" or "section4" or "section5" or "section6" or "section7" or "section8" or "section9" or "section10" or "section11" => section.Trim().ToLowerInvariant(),
            _ => throw new ArgumentException("Invalid storefront section", nameof(section)),
        };
        BsonDocument value = document.TryGetValue(field, out BsonValue existing) && existing.IsBsonDocument ? existing.AsBsonDocument : new BsonDocument();
        if (patch.Name is not null) value["name"] = patch.Name;
        if (patch.NameTranslations is not null) value["nameTranslations"] = ToLocalized(patch.NameTranslations);
        if (patch.ProductIds is not null) value["productId"] = new BsonArray(patch.ProductIds.Select(static x => (BsonValue)x));
        if (patch.Display.HasValue) value["display"] = patch.Display.Value;
        if (patch.Image is not null) value["image"] = patch.Image;
        if (patch.Link is not null) value["link"] = patch.Link;
        document[field] = value;
        document["updatedAt"] = DateTime.UtcNow;
        await SaveAsync(document, cancellationToken);
        return Map(document);
    }

    public async Task<StorefrontContent> UpdateHomeCategoriesAsync(HomeCategoryConfigPatch patch, CancellationToken cancellationToken)
    {
        BsonDocument document = await GetOrCreateAsync(cancellationToken);
        BsonDocument value = document.TryGetValue("homeCategoryConfig", out BsonValue existing) && existing.IsBsonDocument ? existing.AsBsonDocument : new BsonDocument();
        if (patch.Configured.HasValue) value["configured"] = patch.Configured.Value;
        if (patch.SidebarTitle is not null) value["sidebarTitle"] = patch.SidebarTitle;
        if (patch.SidebarTitleTranslations is not null) value["sidebarTitleTranslations"] = ToLocalized(patch.SidebarTitleTranslations);
        if (patch.ShowSidebar.HasValue) value["showSidebar"] = patch.ShowSidebar.Value;
        if (patch.ShowQuickCategories.HasValue) value["showQuickCategories"] = patch.ShowQuickCategories.Value;
        if (patch.Items is not null) value["items"] = new BsonArray(patch.Items.Select(ToCategory));
        document["homeCategoryConfig"] = value;
        document["updatedAt"] = DateTime.UtcNow;
        await SaveAsync(document, cancellationToken);
        return Map(document);
    }

    public async Task<StorefrontContent> UpdatePoliciesAsync(IReadOnlyList<StorefrontPolicy> policies, CancellationToken cancellationToken)
    {
        BsonDocument document = await GetOrCreateAsync(cancellationToken);
        IReadOnlyList<StorefrontPolicy> currentPolicies = MapPolicies(document);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StorefrontPolicy[] timestamped = ResolvePolicyTimestamps(policies, currentPolicies, now);
        document["policies"] = new BsonArray(timestamped.Select(ToPolicy));
        document["updatedAt"] = DateTime.UtcNow;
        await SaveAsync(document, cancellationToken);
        return Map(document);
    }

    public async Task<bool> RemoveImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        BsonDocument? document = await manages.Find(Builders<BsonDocument>.Filter.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (document is null) return false;
        bool found = false;
        foreach (string field in new[] { "overViewImg", "partners" })
        {
            BsonArray values = ReadArray(document, field);
            if (values.Any(v => v.IsString && v.AsString == imageUrl)) { document[field] = new BsonArray(values.Where(v => !v.IsString || v.AsString != imageUrl)); found = true; }
        }
        foreach (string field in new[] { "newProductUrl", "topPurchaseUrl", "highestRatingUrl" })
        {
            if (ReadString(document, field) == imageUrl) { document[field] = string.Empty; found = true; }
        }
        if (found) await SaveAsync(document, cancellationToken);
        return found;
    }

    public async Task<bool> ContainsImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        BsonDocument? document = await manages.Find(Builders<BsonDocument>.Filter.Empty)
            .Project(Builders<BsonDocument>.Projection
                .Include("overViewImg")
                .Include("partners")
                .Include("newProductUrl")
                .Include("topPurchaseUrl")
                .Include("highestRatingUrl")
                .Include("section1").Include("section2").Include("section3")
                .Include("section4").Include("section5").Include("section6")
                .Include("section7").Include("section8").Include("section9")
                .Include("section10").Include("section11"))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null) return false;
        if (ReadStrings(document, "overViewImg").Contains(imageUrl, StringComparer.Ordinal) ||
            ReadStrings(document, "partners").Contains(imageUrl, StringComparer.Ordinal)) return true;
        if (SingleImageFields
            .Any(field => string.Equals(ReadString(document, field), imageUrl, StringComparison.Ordinal))) return true;
        for (int index = 1; index <= 11; index++)
        {
            if (document.TryGetValue($"section{index}", out BsonValue value) && value.IsBsonDocument &&
                string.Equals(ReadString(value.AsBsonDocument, "image"), imageUrl, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private async Task<BsonDocument> GetOrCreateAsync(CancellationToken cancellationToken) => await manages.Find(Builders<BsonDocument>.Filter.Empty).Limit(1).FirstOrDefaultAsync(cancellationToken) ?? new BsonDocument { ["_id"] = ObjectId.GenerateNewId() };
    private async Task<ReplaceOneResult> SaveAsync(BsonDocument document, CancellationToken cancellationToken) => await manages.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), document, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    private static void ApplyPatch(BsonDocument d, StorefrontPatch p)
    {
        if (p.Introduction is not null) d["introduction"] = p.Introduction;
        if (p.IntroductionTranslations is not null) d["introductionTranslations"] = ToLocalized(p.IntroductionTranslations);
        if (p.MainPolicy is not null) d["mainPolicy"] = p.MainPolicy;
        if (p.FooterContent is not null) d["footerContent"] = new BsonDocument { ["logo"] = p.FooterContent.Logo ?? string.Empty, ["description"] = p.FooterContent.Description ?? string.Empty, ["address"] = p.FooterContent.Address ?? string.Empty, ["phone"] = p.FooterContent.Phone ?? string.Empty, ["email"] = p.FooterContent.Email ?? string.Empty };
        if (p.DisplayPartners.HasValue) d["displayPartners"] = p.DisplayPartners.Value;
        if (p.NewProductUrl is not null) d["newProductUrl"] = p.NewProductUrl;
        if (p.TopPurchaseUrl is not null) d["topPurchaseUrl"] = p.TopPurchaseUrl;
        if (p.HighestRatingUrl is not null) d["highestRatingUrl"] = p.HighestRatingUrl;
        if (p.OverviewImages is not null) d["overViewImg"] = new BsonArray(p.OverviewImages.Select(static x => (BsonValue)x));
        if (p.Partners is not null) d["partners"] = new BsonArray(p.Partners.Select(static x => (BsonValue)x));
    }

    private static StorefrontContent Map(BsonDocument d)
    {
        Dictionary<string, StorefrontSection?> sections = new(StringComparer.Ordinal);
        for (int i = 1; i <= 11; i++) sections[$"section{i}"] = d.TryGetValue($"section{i}", out BsonValue value) && value.IsBsonDocument ? MapSection(value.AsBsonDocument) : null;
        return new StorefrontContent(ReadId(d), ReadStrings(d, "overViewImg"), ReadStrings(d, "partners"), ReadBool(d, "displayPartners", true), MapFooter(d), ReadString(d, "newProductUrl"), ReadString(d, "topPurchaseUrl"), ReadString(d, "highestRatingUrl"), ReadString(d, "introduction"), MapLocalized(d, "introductionTranslations"), ReadString(d, "mainPolicy"), MapPolicies(d), MapHome(d), sections, ReadDate(d, "createdAt"), ReadDate(d, "updatedAt"));
    }
    private static StorefrontFooter MapFooter(BsonDocument d) { BsonDocument v = d.TryGetValue("footerContent", out BsonValue x) && x.IsBsonDocument ? x.AsBsonDocument : []; return new StorefrontFooter(ReadString(v, "logo"), ReadString(v, "description"), ReadString(v, "address"), ReadString(v, "phone"), ReadString(v, "email")); }
    private static LocalizedText MapLocalized(BsonDocument d, string field) => d.TryGetValue(field, out BsonValue x) && x.IsBsonDocument ? MapLocalized(x.AsBsonDocument) : new(null, null, null);
    private static LocalizedText MapLocalized(BsonDocument d) => new(ReadString(d, "vi"), ReadString(d, "zh"), ReadString(d, "en"));
    private static StorefrontSection MapSection(BsonDocument d) => new(ReadString(d, "name"), MapLocalized(d, "nameTranslations"), ReadStrings(d, "productId"), ReadBool(d, "display", true), ReadString(d, "image"), ReadString(d, "link"));
    private static HomeCategoryConfig MapHome(BsonDocument d) { BsonDocument v = d.TryGetValue("homeCategoryConfig", out BsonValue x) && x.IsBsonDocument ? x.AsBsonDocument : []; return new HomeCategoryConfig(ReadBool(v, "configured", false), ReadString(v, "sidebarTitle"), MapLocalized(v, "sidebarTitleTranslations"), ReadBool(v, "showSidebar", true), ReadBool(v, "showQuickCategories", true), ReadArray(v, "items").Where(y => y.IsBsonDocument).Select(MapCategory).ToArray()); }
    private static HomeCategoryItem MapCategory(BsonValue x) { BsonDocument d = x.AsBsonDocument; return new HomeCategoryItem(ReadString(d, "id"), ReadString(d, "label"), MapLocalized(d, "labelTranslations"), ReadString(d, "type"), ReadString(d, "link"), ReadString(d, "icon"), ReadString(d, "image"), ReadBool(d, "showSidebar", true), ReadBool(d, "showQuick", true)); }
    private static StorefrontPolicy[] MapPolicies(BsonDocument d) => ReadArray(d, "policies").Where(x => x.IsBsonDocument).Select(MapPolicy).ToArray();
    internal static StorefrontPolicy[] ResolvePolicyTimestamps(
        IReadOnlyList<StorefrontPolicy> incoming,
        IReadOnlyList<StorefrontPolicy> current,
        DateTimeOffset now)
    {
        Dictionary<string, StorefrontPolicy> byKey = current
            .Where(static policy => !string.IsNullOrWhiteSpace(policy.Key))
            .GroupBy(static policy => policy.Key!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        return incoming.Select(policy =>
        {
            if (policy.UpdatedAt.HasValue) return policy;
            if (policy.Key is not null && byKey.TryGetValue(policy.Key, out StorefrontPolicy? existing) &&
                PolicyContentEquals(policy, existing))
                return policy with { UpdatedAt = existing.UpdatedAt ?? now };
            return policy with { UpdatedAt = now };
        }).ToArray();
    }

    private static bool PolicyContentEquals(StorefrontPolicy first, StorefrontPolicy second) =>
        first.Key == second.Key && first.Title == second.Title && first.Summary == second.Summary &&
        PolicySectionsEqual(first.Sections, second.Sections) &&
        PolicyContentEqual(first.Translations.Vietnamese, second.Translations.Vietnamese) &&
        PolicyContentEqual(first.Translations.Chinese, second.Translations.Chinese) &&
        PolicyContentEqual(first.Translations.English, second.Translations.English);

    private static bool PolicyContentEqual(StorefrontPolicyContent? first, StorefrontPolicyContent? second) =>
        first is null || second is null
            ? first is null && second is null
            : first.Title == second.Title && first.Summary == second.Summary &&
              PolicySectionsEqual(first.Sections, second.Sections);

    private static bool PolicySectionsEqual(
        IReadOnlyList<StorefrontPolicySection> first,
        IReadOnlyList<StorefrontPolicySection> second) =>
        first.Count == second.Count && first.Zip(second)
            .All(static pair => pair.First.Title == pair.Second.Title && pair.First.Content == pair.Second.Content);
    internal static StorefrontPolicy MapPolicy(BsonValue x)
    {
        BsonDocument d = x.AsBsonDocument;
        return new StorefrontPolicy(
            ReadString(d, "key"),
            ReadString(d, "title"),
            ReadString(d, "summary"),
            MapPolicySections(d),
            MapPolicyTranslations(d),
            ReadDate(d, "updatedAt"));
    }

    private static StorefrontPolicyTranslations MapPolicyTranslations(BsonDocument d)
    {
        if (!d.TryGetValue("translations", out BsonValue translations) || !translations.IsBsonDocument)
        {
            return new StorefrontPolicyTranslations(null, null, null);
        }

        BsonDocument value = translations.AsBsonDocument;
        return new StorefrontPolicyTranslations(
            MapPolicyContent(value, "vi"),
            MapPolicyContent(value, "zh"),
            MapPolicyContent(value, "en"));
    }

    private static StorefrontPolicyContent? MapPolicyContent(BsonDocument d, string locale) =>
        d.TryGetValue(locale, out BsonValue value) && value.IsBsonDocument
            ? new StorefrontPolicyContent(
                ReadString(value.AsBsonDocument, "title"),
                ReadString(value.AsBsonDocument, "summary"),
                MapPolicySections(value.AsBsonDocument))
            : null;

    private static StorefrontPolicySection[] MapPolicySections(BsonDocument d) =>
        ReadArray(d, "sections")
            .Where(static value => value.IsBsonDocument)
            .Select(static value => new StorefrontPolicySection(
                ReadString(value.AsBsonDocument, "title"),
                ReadString(value.AsBsonDocument, "content")))
            .ToArray();
    private static BsonDocument ToLocalized(LocalizedText x) => new() { ["vi"] = x.Vietnamese ?? string.Empty, ["zh"] = x.Chinese ?? string.Empty, ["en"] = x.English ?? string.Empty };
    private static BsonDocument ToCategory(HomeCategoryItem x) => new() { ["id"] = x.Id ?? string.Empty, ["label"] = x.Label ?? string.Empty, ["labelTranslations"] = ToLocalized(x.LabelTranslations), ["type"] = x.Type ?? string.Empty, ["link"] = x.Link ?? string.Empty, ["icon"] = x.Icon ?? string.Empty, ["image"] = x.Image ?? string.Empty, ["showSidebar"] = x.ShowSidebar, ["showQuick"] = x.ShowQuick };
    internal static BsonDocument ToPolicy(StorefrontPolicy x)
    {
        BsonDocument policy = new()
        {
            ["key"] = x.Key ?? string.Empty,
            ["title"] = x.Title ?? string.Empty,
            ["summary"] = x.Summary ?? string.Empty,
            ["sections"] = ToPolicySections(x.Sections),
            ["translations"] = new BsonDocument(),
        };
        BsonDocument translations = policy["translations"].AsBsonDocument;
        SetPolicyContent(translations, "vi", x.Translations.Vietnamese);
        SetPolicyContent(translations, "zh", x.Translations.Chinese);
        SetPolicyContent(translations, "en", x.Translations.English);
        if (x.UpdatedAt.HasValue) policy["updatedAt"] = x.UpdatedAt.Value.UtcDateTime;
        return policy;
    }

    private static BsonArray ToPolicySections(IEnumerable<StorefrontPolicySection> sections) =>
        new(sections.Select(static section => new BsonDocument
        {
            ["title"] = section.Title ?? string.Empty,
            ["content"] = section.Content ?? string.Empty,
        }));

    private static void SetPolicyContent(BsonDocument translations, string locale, StorefrontPolicyContent? content)
    {
        if (content is null) return;
        translations[locale] = new BsonDocument
        {
            ["title"] = content.Title ?? string.Empty,
            ["summary"] = content.Summary ?? string.Empty,
            ["sections"] = ToPolicySections(content.Sections),
        };
    }
    private static string ReadId(BsonDocument d) => d.TryGetValue("_id", out BsonValue x) && !x.IsBsonNull ? x.ToString() ?? string.Empty : string.Empty;
    private static string? ReadString(BsonDocument d, string f) => d.TryGetValue(f, out BsonValue x) && !x.IsBsonNull ? x.IsString ? x.AsString : x.ToString() : null;
    private static bool ReadBool(BsonDocument d, string f, bool fallback) => d.TryGetValue(f, out BsonValue x) && x.IsBoolean ? x.AsBoolean : fallback;
    private static string[] ReadStrings(BsonDocument d, string f) => ReadArray(d, f).Where(x => x.IsString).Select(x => x.AsString).ToArray();
    private static BsonArray ReadArray(BsonDocument d, string f) => d.TryGetValue(f, out BsonValue x) && x.IsBsonArray ? x.AsBsonArray : [];
    private static DateTimeOffset? ReadDate(BsonDocument d, string f) => d.TryGetValue(f, out BsonValue x) && x.IsValidDateTime ? new DateTimeOffset(x.ToUniversalTime(), TimeSpan.Zero) : null;
}
