using Microsoft.AspNetCore.Mvc;

namespace TTSmartEcom.Api.Contracts.Products;

public sealed class ProductImageUploadRequest
{
    [FromForm(Name = "product")]
    public IFormFile? Product { get; set; }
}

public sealed class ProductDocumentUploadRequest
{
    [FromForm(Name = "document")]
    public IFormFile? Document { get; set; }
}

public sealed class SectionImageUploadRequest
{
    [FromForm(Name = "sectionImage")]
    public IFormFile? SectionImage { get; set; }
}
