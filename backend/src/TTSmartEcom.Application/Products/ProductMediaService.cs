using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Application.Products;

public sealed class ProductMediaService(IProductMediaRepository repository)
{
    public async Task<ProductMediaMutationResult> PrepareVariantImageDeletionAsync(
        string? productId,
        int variantIndex,
        CancellationToken cancellationToken)
    {
        if (!IsIdentifier(productId))
        {
            return new ProductMediaMutationResult(ProductMutationStatus.NotFound, Message: "Product not found");
        }

        ProductVariantImageReference? reference = await repository.GetVariantImageReferenceAsync(
            productId!.Trim(), variantIndex, cancellationToken);
        if (reference is null)
        {
            return new ProductMediaMutationResult(ProductMutationStatus.NotFound, Message: "Product not found");
        }

        if (!reference.VariantExists)
        {
            return new ProductMediaMutationResult(ProductMutationStatus.NotFound, Message: "Variant not found");
        }

        if (string.IsNullOrWhiteSpace(reference.ImageUrl))
        {
            return new ProductMediaMutationResult(ProductMutationStatus.Invalid, Message: "No image to delete");
        }

        return new ProductMediaMutationResult(ProductMutationStatus.Success, ImageUrl: reference.ImageUrl);
    }

    public Task<bool> IsProductImageReferencedElsewhereAsync(
        string productId,
        int variantIndex,
        string filename,
        CancellationToken cancellationToken) =>
        repository.IsProductImageReferencedElsewhereAsync(productId, variantIndex, filename, cancellationToken);

    public async Task<ProductMediaMutationResult> ClearVariantImageAsync(
        string productId,
        int variantIndex,
        string expectedImageUrl,
        CancellationToken cancellationToken)
    {
        ProductRecord? product = await repository.ClearVariantImageAsync(
            productId, variantIndex, expectedImageUrl, cancellationToken);
        return product is null
            ? new ProductMediaMutationResult(ProductMutationStatus.Conflict, Message: "Product image changed")
            : new ProductMediaMutationResult(ProductMutationStatus.Success, Product: product);
    }

    public Task<bool> IsInvoiceImageReferencedAsync(string filename, CancellationToken cancellationToken) =>
        repository.IsInvoiceImageReferencedAsync(filename, cancellationToken);

    private static bool IsIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100 &&
        value.Trim().All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
}
