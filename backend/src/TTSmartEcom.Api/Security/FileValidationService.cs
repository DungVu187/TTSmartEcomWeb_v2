using TTSmartEcom.Application.Abstractions.Files;

namespace TTSmartEcom.Api.Security;

public sealed class FileValidationService : IFileValidationService
{
    private static readonly Dictionary<FileUploadKind, FilePolicy> Policies =
        new Dictionary<FileUploadKind, FilePolicy>
        {
            [FileUploadKind.ProductImage] = new(4, [".jpg", ".jpeg", ".png", ".webp"], ["image/jpeg", "image/jpg", "image/png", "image/webp"], [[0xFF, 0xD8, 0xFF], [0x89, 0x50, 0x4E, 0x47], [0x52, 0x49, 0x46, 0x46]]),
            [FileUploadKind.ProductDocument] = new(20, [".pdf"], ["application/pdf"], [[0x25, 0x50, 0x44, 0x46]]),
            [FileUploadKind.Invoice] = new(5, [".jpg", ".jpeg", ".png", ".webp"], ["image/jpeg", "image/jpg", "image/png", "image/webp"], [[0xFF, 0xD8, 0xFF], [0x89, 0x50, 0x4E, 0x47], [0x52, 0x49, 0x46, 0x46]]),
            [FileUploadKind.VoiceAudio] = new(10, [".webm", ".wav", ".mp3", ".ogg", ".m4a"], ["audio/webm", "audio/wav", "audio/mpeg", "audio/ogg", "audio/mp4", "application/octet-stream"], []),
            [FileUploadKind.StationImage] = new(5, [".jpg", ".jpeg", ".png", ".webp"], ["image/jpeg", "image/jpg", "image/png", "image/webp"], [[0xFF, 0xD8, 0xFF], [0x89, 0x50, 0x4E, 0x47], [0x52, 0x49, 0x46, 0x46]]),
            [FileUploadKind.StorefrontImage] = new(5, [".jpg", ".jpeg", ".png", ".webp"], ["image/jpeg", "image/jpg", "image/png", "image/webp"], [[0xFF, 0xD8, 0xFF], [0x89, 0x50, 0x4E, 0x47], [0x52, 0x49, 0x46, 0x46]]),
        };

    public FileValidationResult Validate(string fileName, string contentType, long length, Stream content, FileUploadKind kind)
    {
        if (string.IsNullOrWhiteSpace(fileName) || length <= 0 || content is null)
        {
            return FileValidationResult.Invalid("TTS-UPLOAD-0001", "File is required");
        }

        if (fileName.Length > 180 || fileName.Contains("..", StringComparison.Ordinal) || Path.GetFileName(fileName) != fileName)
        {
            return FileValidationResult.Invalid("TTS-UPLOAD-0002", "Invalid file name");
        }

        FilePolicy policy = Policies[kind];
        if (length > policy.MaxBytes)
        {
            return FileValidationResult.Invalid("TTS-UPLOAD-0003", "File is too large");
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!policy.Extensions.Contains(extension, StringComparer.Ordinal))
        {
            return FileValidationResult.Invalid("TTS-UPLOAD-0004", "File extension is not allowed");
        }

        if (!policy.ContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return FileValidationResult.Invalid("TTS-UPLOAD-0005", "File content type is not allowed");
        }

        if (policy.Signatures.Length > 0)
        {
            byte[] header = new byte[16];
            int read = content.Read(header, 0, header.Length);
            if (!policy.Signatures.Any(signature => read >= signature.Length && signature.AsSpan().SequenceEqual(header.AsSpan(0, signature.Length))))
            {
                return FileValidationResult.Invalid("TTS-UPLOAD-0006", "File signature is invalid");
            }
        }

        return FileValidationResult.Valid();
    }

    private sealed record FilePolicy(long MaxMegabytes, string[] Extensions, string[] ContentTypes, byte[][] Signatures)
    {
        public long MaxBytes => MaxMegabytes * 1024 * 1024;
    }
}
