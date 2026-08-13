using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Files;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Files;

namespace TTSmartEcom.UnitTests.Files;

public sealed class LocalMediaFileServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"ttsmart-media-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_WhenInvoiceIsValid_ShouldGenerateSafeNameAndPersistContent()
    {
        LocalMediaFileService service = CreateService();
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A];
        using MemoryStream content = new(bytes);
        FormFile file = new(content, 0, content.Length, "invoice", "receipt.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };

        LocalMediaSaveResult result = await service.SaveAsync(
            file,
            FileUploadKind.Invoice,
            "invoices",
            "invoice-sale-",
            "invoice-images",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Matches("^/invoice-images/invoice-sale-[0-9]+-[0-9]+[.]png$", result.PublicUrl);
        string path = Path.Combine(root, "invoices", result.FileName!);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task SaveAsync_WhenFilenameHasDangerousInnerExtension_ShouldRejectWithoutWritingFile()
    {
        LocalMediaFileService service = CreateService();
        using MemoryStream content = new([0x89, 0x50, 0x4E, 0x47]);
        FormFile file = new(content, 0, content.Length, "invoice", "payload.php.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };

        LocalMediaSaveResult result = await service.SaveAsync(
            file,
            FileUploadKind.Invoice,
            "invoices",
            "invoice-sale-",
            "invoice-images",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TTS-UPLOAD-0002", result.ErrorCode);
        Assert.False(Directory.Exists(Path.Combine(root, "invoices")));
    }

    [Theory]
    [InlineData("/images/unrelated.png")]
    [InlineData("/invoice-images/%2e%2e%2fescape.png")]
    [InlineData("file:///invoice-images/receipt.png")]
    public void Delete_WhenUrlIsOutsideExpectedRoute_ShouldReject(string imageUrl)
    {
        LocalMediaDeleteResult result = CreateService().Delete(imageUrl, "invoice-images", "invoices");

        Assert.False(result.IsValid);
        Assert.False(result.Deleted);
    }

    [Fact]
    public void Delete_WhenFileIsInsideConfiguredDirectory_ShouldDeleteIt()
    {
        string directory = Path.Combine(root, "invoices");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "invoice-sale-1-1.webp");
        File.WriteAllBytes(path, [0x52, 0x49, 0x46, 0x46]);

        LocalMediaDeleteResult result = CreateService().Delete(
            "/invoice-images/invoice-sale-1-1.webp",
            "invoice-images",
            "invoices");

        Assert.True(result.IsValid);
        Assert.True(result.Deleted);
        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        GC.SuppressFinalize(this);
    }

    private LocalMediaFileService CreateService()
    {
        UploadOptions options = new() { RootPath = root };
        return new LocalMediaFileService(
            new FileValidationService(),
            Options.Create(options),
            new TestHostEnvironment(root));
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "TTSmartEcom.UnitTests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
