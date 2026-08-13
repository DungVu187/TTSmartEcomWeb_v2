using TTSmartEcom.Domain.Storefront;

namespace TTSmartEcom.Application.Storefront;

public interface IStorefrontRepository
{
    Task<StorefrontContent?> GetAsync(CancellationToken cancellationToken);
    Task<StorefrontContent> UpsertAsync(StorefrontPatch patch, CancellationToken cancellationToken);
    Task<StorefrontContent> UpdateSectionAsync(string section, StorefrontSectionPatch patch, CancellationToken cancellationToken);
    Task<StorefrontContent> UpdateHomeCategoriesAsync(HomeCategoryConfigPatch patch, CancellationToken cancellationToken);
    Task<StorefrontContent> UpdatePoliciesAsync(IReadOnlyList<StorefrontPolicy> policies, CancellationToken cancellationToken);
    Task<bool> RemoveImageAsync(string imageUrl, CancellationToken cancellationToken);
    Task<bool> ContainsImageAsync(string imageUrl, CancellationToken cancellationToken);
}

public sealed record StorefrontPatch(
    string? Introduction = null,
    LocalizedText? IntroductionTranslations = null,
    string? MainPolicy = null,
    StorefrontFooter? FooterContent = null,
    bool? DisplayPartners = null,
    string? NewProductUrl = null,
    string? TopPurchaseUrl = null,
    string? HighestRatingUrl = null,
    IReadOnlyList<string>? OverviewImages = null,
    IReadOnlyList<string>? Partners = null);

public sealed record StorefrontSectionPatch(string? Name, LocalizedText? NameTranslations, IReadOnlyList<string>? ProductIds,
    bool? Display, string? Image, string? Link);

public sealed record HomeCategoryConfigPatch(bool? Configured, string? SidebarTitle, LocalizedText? SidebarTitleTranslations,
    bool? ShowSidebar, bool? ShowQuickCategories, IReadOnlyList<HomeCategoryItem>? Items);
