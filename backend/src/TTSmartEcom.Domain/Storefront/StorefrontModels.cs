using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Storefront;

public sealed record StorefrontContent(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("overViewImg")] IReadOnlyList<string> OverviewImages,
    IReadOnlyList<string> Partners,
    bool DisplayPartners,
    StorefrontFooter FooterContent,
    string? NewProductUrl,
    string? TopPurchaseUrl,
    string? HighestRatingUrl,
    string? Introduction,
    LocalizedText IntroductionTranslations,
    string? MainPolicy,
    IReadOnlyList<StorefrontPolicy> Policies,
    HomeCategoryConfig HomeCategoryConfig,
    [property: JsonIgnore] IReadOnlyDictionary<string, StorefrontSection?> Sections,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    [JsonPropertyName("section1")] public StorefrontSection? Section1 => Sections.GetValueOrDefault("section1");
    [JsonPropertyName("section2")] public StorefrontSection? Section2 => Sections.GetValueOrDefault("section2");
    [JsonPropertyName("section3")] public StorefrontSection? Section3 => Sections.GetValueOrDefault("section3");
    [JsonPropertyName("section4")] public StorefrontSection? Section4 => Sections.GetValueOrDefault("section4");
    [JsonPropertyName("section5")] public StorefrontSection? Section5 => Sections.GetValueOrDefault("section5");
    [JsonPropertyName("section6")] public StorefrontSection? Section6 => Sections.GetValueOrDefault("section6");
    [JsonPropertyName("section7")] public StorefrontSection? Section7 => Sections.GetValueOrDefault("section7");
    [JsonPropertyName("section8")] public StorefrontSection? Section8 => Sections.GetValueOrDefault("section8");
    [JsonPropertyName("section9")] public StorefrontSection? Section9 => Sections.GetValueOrDefault("section9");
    [JsonPropertyName("section10")] public StorefrontSection? Section10 => Sections.GetValueOrDefault("section10");
    [JsonPropertyName("section11")] public StorefrontSection? Section11 => Sections.GetValueOrDefault("section11");
}

public sealed record StorefrontFooter(string? Logo, string? Description, string? Address, string? Phone, string? Email);

public sealed record LocalizedText(
    [property: JsonPropertyName("vi")] string? Vietnamese,
    [property: JsonPropertyName("zh")] string? Chinese,
    [property: JsonPropertyName("en")] string? English);

public sealed record HomeCategoryConfig(bool Configured, string? SidebarTitle, LocalizedText SidebarTitleTranslations,
    bool ShowSidebar, bool ShowQuickCategories, IReadOnlyList<HomeCategoryItem> Items);

public sealed record HomeCategoryItem(string? Id, string? Label, LocalizedText LabelTranslations, string? Type,
    string? Link, string? Icon, string? Image, bool ShowSidebar, bool ShowQuick);

public sealed record StorefrontSection(string? Name, LocalizedText NameTranslations, [property: JsonPropertyName("productId")] IReadOnlyList<string> ProductIds,
    bool Display, string? Image, string? Link);

public sealed record StorefrontPolicy(string? Key, string? Title, string? Summary, IReadOnlyList<StorefrontPolicySection> Sections,
    StorefrontPolicyTranslations Translations, DateTimeOffset? UpdatedAt);

public sealed record StorefrontPolicyTranslations(
    [property: JsonPropertyName("vi")] StorefrontPolicyContent? Vietnamese,
    [property: JsonPropertyName("zh")] StorefrontPolicyContent? Chinese,
    [property: JsonPropertyName("en")] StorefrontPolicyContent? English);

public sealed record StorefrontPolicyContent(string? Title, string? Summary, IReadOnlyList<StorefrontPolicySection> Sections);

public sealed record StorefrontPolicySection(string? Title, string? Content);
