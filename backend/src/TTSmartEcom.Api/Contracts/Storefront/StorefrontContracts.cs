using TTSmartEcom.Domain.Storefront;

namespace TTSmartEcom.Api.Contracts.Storefront;

public sealed record StorefrontPatchRequest(string? Introduction, LocalizedText? IntroductionTranslations, string? MainPolicy, StorefrontFooter? FooterContent, bool? DisplayPartners, string? NewProductUrl, string? TopPurchaseUrl, string? HighestRatingUrl, IReadOnlyList<string>? OverviewImages, IReadOnlyList<string>? Partners);
public sealed record PoliciesRequest(IReadOnlyList<StorefrontPolicy>? Policies);
public sealed record DeleteStorefrontImageRequest(string? ImgUrl, string? ImageUrl);

public sealed class StorefrontSingleImageRequest
{
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "manage")]
    public List<Microsoft.AspNetCore.Http.IFormFile>? Manage { get; set; }

    [Microsoft.AspNetCore.Mvc.FromForm(Name = "topPurchaseUrl")]
    public bool TopPurchaseUrl { get; set; }

    [Microsoft.AspNetCore.Mvc.FromForm(Name = "highestRatingUrl")]
    public bool HighestRatingUrl { get; set; }
}

public sealed class StorefrontImagesRequest
{
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "manage")]
    public List<Microsoft.AspNetCore.Http.IFormFile>? Manage { get; set; }
}

public sealed class StorefrontSectionImageRequest
{
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "image")]
    public Microsoft.AspNetCore.Http.IFormFile? Image { get; set; }
}
