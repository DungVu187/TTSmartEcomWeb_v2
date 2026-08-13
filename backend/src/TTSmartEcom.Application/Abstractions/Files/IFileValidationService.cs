namespace TTSmartEcom.Application.Abstractions.Files;

public interface IFileValidationService
{
    FileValidationResult Validate(string fileName, string contentType, long length, Stream content, FileUploadKind kind);
}

public enum FileUploadKind
{
    ProductImage,
    ProductDocument,
    Invoice,
    VoiceAudio,
    StationImage,
    StorefrontImage,
}

public sealed record FileValidationResult(bool IsValid, string? ErrorCode, string? Message)
{
    public static FileValidationResult Valid() => new(true, null, null);

    public static FileValidationResult Invalid(string code, string message) => new(false, code, message);
}
