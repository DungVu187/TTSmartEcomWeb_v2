using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Application.Abstractions.Files;

namespace TTSmartEcom.Api.Files;

public sealed class LocalMediaFileService(
    IFileValidationService fileValidation,
    IOptions<UploadOptions> uploadOptions,
    IWebHostEnvironment environment)
{
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] DangerousInnerExtensions =
        [".aspx", ".bat", ".cmd", ".dll", ".exe", ".html", ".js", ".php", ".ps1", ".sh", ".svg"];

    public async Task<LocalMediaSaveResult> SaveAsync(
        IFormFile file,
        FileUploadKind kind,
        string directoryName,
        string filePrefix,
        string publicPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (HasDangerousInnerExtension(file.FileName))
        {
            return LocalMediaSaveResult.Invalid("TTS-UPLOAD-0002", "Invalid file name");
        }

        await using Stream validationContent = file.OpenReadStream();
        FileValidationResult validation = fileValidation.Validate(
            file.FileName,
            file.ContentType,
            file.Length,
            validationContent,
            kind);
        if (!validation.IsValid)
        {
            return LocalMediaSaveResult.Invalid(
                validation.ErrorCode ?? "TTS-UPLOAD-0000",
                validation.Message ?? "File is invalid");
        }

        string directory = UploadPathResolver.ResolveSubdirectory(uploadOptions.Value, environment, directoryName);
        Directory.CreateDirectory(directory);
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            string fileName = CreateStorageName(filePrefix, extension);
            string filePath = ResolveContainedFile(directory, fileName);
            try
            {
                await using FileStream destination = new(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous);
                await using Stream source = file.OpenReadStream();
                await source.CopyToAsync(destination, cancellationToken);
                return LocalMediaSaveResult.Saved(
                    fileName,
                    $"/{publicPath.Trim('/')}/{Uri.EscapeDataString(fileName)}");
            }
            catch (IOException) when (File.Exists(filePath) && attempt < 2)
            {
                // A generated name collision is retried with fresh cryptographic randomness.
            }
            catch
            {
                TryDeletePartialFile(filePath);
                throw;
            }
        }

        throw new IOException("Could not allocate a unique upload storage name.");
    }

    public LocalMediaDeleteResult Delete(string imageUrl, string publicPath, string directoryName)
    {
        if (!TryExtractFileName(imageUrl, publicPath, out string? fileName) || fileName is null)
        {
            return LocalMediaDeleteResult.Invalid();
        }

        string directory = UploadPathResolver.ResolveSubdirectory(uploadOptions.Value, environment, directoryName);
        string filePath = ResolveContainedFile(directory, fileName);
        if (!File.Exists(filePath))
        {
            return LocalMediaDeleteResult.Missing(fileName);
        }

        File.Delete(filePath);
        return LocalMediaDeleteResult.DeletedFile(fileName);
    }

    private static string CreateStorageName(string prefix, string extension)
    {
        if (string.IsNullOrWhiteSpace(prefix)
            || prefix.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("Storage prefix is invalid.", nameof(prefix));
        }

        int randomSuffix = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000);
        return $"{prefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{randomSuffix}{extension}";
    }

    private static string ResolveContainedFile(string directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidOperationException("Media file name is invalid.");
        }

        string path = Path.GetFullPath(Path.Combine(directory, fileName));
        string relative = Path.GetRelativePath(directory, path);
        if (Path.IsPathFullyQualified(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Media path escapes the configured directory.");
        }

        return path;
    }

    private static bool TryExtractFileName(string imageUrl, string publicPath, out string? fileName)
    {
        fileName = null;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return false;
        }

        string path;
        if (Uri.TryCreate(imageUrl.Trim(), UriKind.Absolute, out Uri? absoluteUri))
        {
            if (absoluteUri.Scheme is not ("http" or "https"))
            {
                return false;
            }
            path = absoluteUri.AbsolutePath;
        }
        else
        {
            string value = imageUrl.Trim();
            int suffixIndex = value.IndexOfAny(['?', '#']);
            path = suffixIndex < 0 ? value : value[..suffixIndex];
        }

        string expectedPrefix = $"/{publicPath.Trim('/')}";
        if (!path.StartsWith($"{expectedPrefix}/", StringComparison.Ordinal))
        {
            return false;
        }

        string encodedName = path[(expectedPrefix.Length + 1)..];
        if (encodedName.Length == 0 || encodedName.Contains('/') || encodedName.Contains('\\'))
        {
            return false;
        }

        try
        {
            string decodedName = Uri.UnescapeDataString(encodedName);
            if (decodedName.Length == 0
                || decodedName.Contains("..", StringComparison.Ordinal)
                || decodedName.Contains('/')
                || decodedName.Contains('\\')
                || Path.GetFileName(decodedName) != decodedName)
            {
                return false;
            }

            string extension = Path.GetExtension(decodedName);
            if (!AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            fileName = decodedName;
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool HasDangerousInnerExtension(string fileName)
    {
        string withoutFinalExtension = Path.GetFileNameWithoutExtension(fileName);
        string innerExtension = Path.GetExtension(withoutFinalExtension);
        return DangerousInnerExtensions.Contains(innerExtension, StringComparer.OrdinalIgnoreCase);
    }

    private static void TryDeletePartialFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
            // Preserve the original upload failure. Operational cleanup can remove a partial file.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the original upload failure without exposing the physical storage path.
        }
    }
}

public sealed record LocalMediaSaveResult(
    bool IsSuccess,
    string? FileName,
    string? PublicUrl,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static LocalMediaSaveResult Saved(string fileName, string publicUrl) =>
        new(true, fileName, publicUrl, null, null);

    public static LocalMediaSaveResult Invalid(string errorCode, string message) =>
        new(false, null, null, errorCode, message);
}

public sealed record LocalMediaDeleteResult(bool IsValid, bool Deleted, string? FileName)
{
    public static LocalMediaDeleteResult Invalid() => new(false, false, null);
    public static LocalMediaDeleteResult Missing(string fileName) => new(true, false, fileName);
    public static LocalMediaDeleteResult DeletedFile(string fileName) => new(true, true, fileName);
}
