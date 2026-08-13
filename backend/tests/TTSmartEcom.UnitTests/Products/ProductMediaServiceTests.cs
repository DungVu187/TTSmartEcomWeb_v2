using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductMediaServiceTests
{
    [Fact]
    public async Task PrepareVariantImageDeletion_WhenImageExists_ShouldReturnStoredUrl()
    {
        FakeRepository repository = new()
        {
            Reference = new ProductVariantImageReference(true, "/images/product_1700000000000.webp"),
        };
        ProductMediaService service = new(repository);

        ProductMediaMutationResult result = await service.PrepareVariantImageDeletionAsync(
            "507f1f77bcf86cd799439011", 0, CancellationToken.None);

        Assert.Equal(ProductMutationStatus.Success, result.Status);
        Assert.Equal("/images/product_1700000000000.webp", result.ImageUrl);
    }

    [Fact]
    public async Task PrepareVariantImageDeletion_WhenVariantHasNoImage_ShouldReject()
    {
        FakeRepository repository = new() { Reference = new ProductVariantImageReference(true, string.Empty) };
        ProductMediaService service = new(repository);

        ProductMediaMutationResult result = await service.PrepareVariantImageDeletionAsync(
            "507f1f77bcf86cd799439011", 0, CancellationToken.None);

        Assert.Equal(ProductMutationStatus.Invalid, result.Status);
        Assert.Equal("No image to delete", result.Message);
    }

    [Fact]
    public async Task ClearVariantImage_WhenRepositoryCasFails_ShouldReturnConflict()
    {
        ProductMediaService service = new(new FakeRepository());

        ProductMediaMutationResult result = await service.ClearVariantImageAsync(
            "507f1f77bcf86cd799439011", 0, "/images/product_1700000000000.webp", CancellationToken.None);

        Assert.Equal(ProductMutationStatus.Conflict, result.Status);
    }

    private sealed class FakeRepository : IProductMediaRepository
    {
        public ProductVariantImageReference? Reference { get; init; }
        public ProductRecord? ClearedProduct { get; init; }

        public Task<ProductVariantImageReference?> GetVariantImageReferenceAsync(
            string productId, int variantIndex, CancellationToken cancellationToken) => Task.FromResult(Reference);

        public Task<bool> IsProductImageReferencedElsewhereAsync(
            string productId, int variantIndex, string filename, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<ProductRecord?> ClearVariantImageAsync(
            string productId, int variantIndex, string expectedImageUrl, CancellationToken cancellationToken) =>
            Task.FromResult(ClearedProduct);

        public Task<bool> IsInvoiceImageReferencedAsync(string filename, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
