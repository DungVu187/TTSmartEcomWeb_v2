using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Controllers.Products;

namespace TTSmartEcom.UnitTests.Products;

public sealed class ProductMediaFileServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"ttsmart-media-{Guid.NewGuid():N}");

    [Fact]
    public void ResolveProductImage_WhenPathTraverses_ShouldReject()
    {
        ProductMediaFileService service = CreateService();

        ProductMediaFileException error = Assert.Throws<ProductMediaFileException>(
            () => service.ResolveProductImage("/images/%2e%2e%2foutside.webp"));

        Assert.Equal("Invalid image path", error.Message);
    }

    [Fact]
    public void ResolveTemporaryInvoiceImage_WhenNamespaceIsNotScanTemp_ShouldReject()
    {
        ProductMediaFileService service = CreateService();

        ProductMediaFileException error = Assert.Throws<ProductMediaFileException>(
            () => service.ResolveTemporaryInvoiceImage("/invoice-images/invoice-manual-1700000000000-1.webp"));

        Assert.Equal("Đường dẫn ảnh tạm không hợp lệ.", error.Message);
    }

    [Fact]
    public async Task DeleteRegularFileIfExists_WhenFileIsContained_ShouldDelete()
    {
        ProductMediaFileService service = CreateService();
        string directory = Path.Combine(root, "images");
        Directory.CreateDirectory(directory);
        string filename = "product_1700000000000.webp";
        await File.WriteAllBytesAsync(Path.Combine(directory, filename), [0x52, 0x49, 0x46, 0x46]);
        ResolvedMediaFile resolved = service.ResolveProductImage($"/images/{filename}");

        bool deleted = await service.DeleteRegularFileIfExistsAsync(resolved, CancellationToken.None);

        Assert.True(deleted);
        Assert.False(File.Exists(resolved.FullPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private ProductMediaFileService CreateService() => new(
        Options.Create(new UploadOptions { RootPath = root }),
        new FakeEnvironment(root),
        TimeProvider.System);

    private sealed class FakeEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "TTSmartEcom.UnitTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
