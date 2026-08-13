using TTSmartEcom.Domain.Storefront;

namespace TTSmartEcom.Api.Contracts.Storefront.Requests;

public sealed record UpdateStorefrontRequest(string? Introduction, LocalizedText? Translations, string? MainPolicy, StorefrontFooter? FooterContent,
    bool? DisplayPartners, string? NewProductUrl, string? TopPurchaseUrl, string? HighestRatingUrl);
public sealed record UpdateStorefrontSectionRequest(string? Name, LocalizedText? NameTranslations, IReadOnlyList<string>? ProductId, bool? Display, string? Image, string? Link);
public sealed record UpdateHomeCategoriesRequest(bool? Configured, string? SidebarTitle, LocalizedText? SidebarTitleTranslations, bool? ShowSidebar, bool? ShowQuickCategories, IReadOnlyList<HomeCategoryItem>? Items);
public sealed record UpdatePoliciesRequest(IReadOnlyList<StorefrontPolicy>? Policies);
