using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Files;

namespace TTSmartEcom.UnitTests.Security;

public sealed class FileValidationServiceTests
{
    [Fact]
    public void Validate_WhenPdfSignatureMatches_ShouldAccept()
    {
        FileValidationService service = new();
        using MemoryStream content = new([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31]);

        FileValidationResult result = service.Validate("invoice.pdf", "application/pdf", content.Length, content, FileUploadKind.ProductDocument);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenExtensionAndMimeDisagree_ShouldReject()
    {
        FileValidationService service = new();
        using MemoryStream content = new([0x25, 0x50, 0x44, 0x46]);

        FileValidationResult result = service.Validate("invoice.exe", "application/pdf", content.Length, content, FileUploadKind.ProductDocument);

        Assert.False(result.IsValid);
        Assert.Equal("TTS-UPLOAD-0004", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenFilenameTraversesPath_ShouldReject()
    {
        FileValidationService service = new();
        using MemoryStream content = new([0xFF, 0xD8, 0xFF]);

        FileValidationResult result = service.Validate("..\\secret.jpg", "image/jpeg", content.Length, content, FileUploadKind.ProductImage);

        Assert.False(result.IsValid);
        Assert.Equal("TTS-UPLOAD-0002", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenImageSignatureDoesNotMatch_ShouldReject()
    {
        FileValidationService service = new();
        using MemoryStream content = new([0x4D, 0x5A, 0x90, 0x00]);

        FileValidationResult result = service.Validate("image.jpg", "image/jpeg", content.Length, content, FileUploadKind.ProductImage);

        Assert.False(result.IsValid);
        Assert.Equal("TTS-UPLOAD-0006", result.ErrorCode);
    }

    [Fact]
    public void Validate_WhenFileExceedsLimit_ShouldRejectWithoutReadingContent()
    {
        FileValidationService service = new();
        using MemoryStream content = new([0xFF, 0xD8, 0xFF]);

        FileValidationResult result = service.Validate("image.jpg", "image/jpeg", 5L * 1024 * 1024, content, FileUploadKind.ProductImage);

        Assert.False(result.IsValid);
        Assert.Equal("TTS-UPLOAD-0003", result.ErrorCode);
    }
}
