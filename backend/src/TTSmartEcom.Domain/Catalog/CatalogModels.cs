using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Catalog;

public sealed record BrandRecord(string Id, string? Brand);

public sealed record ChipValuesRecord(
    IReadOnlyList<string> Color,
    IReadOnlyList<string> Shapes,
    IReadOnlyList<string> Frames,
    IReadOnlyList<string> ButtonCount);

public sealed record SectionItemRecord(
    string? Id,
    string? Name,
    IReadOnlyList<string> Values,
    string? ImageUrl);

public sealed record SectionDocumentRecord(string Id, IReadOnlyList<SectionItemRecord> Sections);

public sealed record ManageFooterRecord(
    string? Logo,
    string? Description,
    string? Address,
    string? Phone,
    string? Email);

public sealed record LocalizedTextRecord(
    [property: JsonPropertyName("vi")] string? Vietnamese,
    [property: JsonPropertyName("zh")] string? Chinese,
    [property: JsonPropertyName("en")] string? English);

public sealed record ManageSectionRecord(
    string? Name,
    LocalizedTextRecord? NameTranslations,
    IReadOnlyList<string> ProductIds,
    bool? Display,
    string? Image,
    string? Link);

public sealed record ManageCategoryItemRecord(
    string? Id,
    string? Label,
    LocalizedTextRecord? LabelTranslations,
    string? Type,
    string? Link,
    string? Icon,
    string? Image,
    bool? ShowSidebar,
    bool? ShowQuick);

public sealed record ManageHomeCategoryRecord(
    bool? Configured,
    string? SidebarTitle,
    LocalizedTextRecord? SidebarTitleTranslations,
    bool? ShowSidebar,
    bool? ShowQuickCategories,
    IReadOnlyList<ManageCategoryItemRecord> Items);

public sealed record ManagePolicySectionRecord(string? Title, string? Content);

public sealed record ManagePolicyRecord(
    string? Key,
    string? Title,
    string? Summary,
    IReadOnlyList<ManagePolicySectionRecord> Sections,
    DateTimeOffset? UpdatedAt);

public sealed record ManageRecord(
    string Id,
    IReadOnlyList<string> OverviewImages,
    IReadOnlyList<string> Partners,
    bool? DisplayPartners,
    ManageFooterRecord? FooterContent,
    string? NewProductUrl,
    string? TopPurchaseUrl,
    string? HighestRatingUrl,
    string? Introduction,
    LocalizedTextRecord? IntroductionTranslations,
    string? MainPolicy,
    IReadOnlyList<ManagePolicyRecord> Policies,
    ManageHomeCategoryRecord? HomeCategoryConfig,
    IReadOnlyDictionary<string, ManageSectionRecord> Sections,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);
