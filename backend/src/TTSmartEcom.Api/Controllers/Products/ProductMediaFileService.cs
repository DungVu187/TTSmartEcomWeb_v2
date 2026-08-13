using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;

namespace TTSmartEcom.Api.Controllers.Products;

public enum ProductMediaFileKind
{
    ProductImage,
    ProductDocument,
    SectionImage,
}

public sealed record StoredMediaFile(string FileName, string FullPath);

public sealed record ResolvedMediaFile(string FileName, string FullPath, string InvalidPathMessage);

public sealed class ProductMediaFileException(string message, int statusCode = 400, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class ProductMediaFileService(
    IOptions<UploadOptions> options,
    IHostEnvironment environment,
    TimeProvider timeProvider)
{
    private static readonly HashSet<string> ProductImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".jfif" };
    private readonly string root = UploadPathResolver.ResolveRoot(options.Value, environment);

    public async Task<StoredMediaFile> SaveAsync(
        IFormFile file,
        ProductMediaFileKind kind,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string prefix = kind switch
        {
            ProductMediaFileKind.ProductImage => "product",
            ProductMediaFileKind.ProductDocument => "document",
            ProductMediaFileKind.SectionImage => "sectionImage",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string directory = kind switch
        {
            ProductMediaFileKind.ProductImage => "images",
            ProductMediaFileKind.ProductDocument => "documents",
            ProductMediaFileKind.SectionImage => "sections",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string filename = $"{prefix}_{timeProvider.GetUtcNow().ToUnixTimeMilliseconds()}{RandomNumberGenerator.GetInt32(1_000, 10_000)}{extension}";
        string folder = ResolveContained(root, directory, "Invalid upload path");
        Directory.CreateDirectory(folder);
        string fullPath = ResolveContained(folder, filename, "Invalid upload path");

        await using FileStream target = new(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await file.CopyToAsync(target, cancellationToken);
        return new StoredMediaFile(filename, fullPath);
    }

    public ResolvedMediaFile ResolveProductImage(string imageUrl)
    {
        const string error = "Invalid image path";
        string filename = ResolveUrlFilename(imageUrl, "images", allowApiPrefix: true, error);
        if (!TryMatchGeneratedFilename(filename, "product", ProductImageExtensions))
            throw new ProductMediaFileException(error);
        return new ResolvedMediaFile(filename, ResolveContained(ResolveContained(root, "images", error), filename, error), error);
    }

    public ResolvedMediaFile ResolveTemporaryInvoiceImage(string imageUrl)
    {
        const string error = "Đường dẫn ảnh tạm không hợp lệ.";
        string filename = ResolveUrlFilename(imageUrl, "invoice-images", allowApiPrefix: true, error);
        if (!System.Text.RegularExpressions.Regex.IsMatch(filename, "^invoice-scan-[0-9]+-[0-9]+[.](?:jpg|jpeg|png|webp)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new ProductMediaFileException(error);
        return new ResolvedMediaFile(filename, ResolveContained(ResolveContained(root, "invoices", error), filename, error), error);
    }

    public ResolvedMediaFile ResolveSectionImageFilename(string filename)
    {
        const string error = "Tên file ảnh phân loại không hợp lệ";
        if (filename.Length == 0 || filename.Length > 180 || filename.Contains('\0') ||
            !string.Equals(filename, Path.GetFileName(filename), StringComparison.Ordinal) ||
            !TryMatchGeneratedFilename(filename, "sectionImage", ProductImageExtensions.Where(value => value != ".jfif")))
            throw new ProductMediaFileException(error);
        return new ResolvedMediaFile(filename, ResolveContained(ResolveContained(root, "sections", error), filename, error), error);
    }

    public async Task<bool> DeleteRegularFileIfExistsAsync(ResolvedMediaFile file, CancellationToken cancellationToken)
    {
        try
        {
            FileInfo info = new(file.FullPath);
            if (!info.Exists) return false;
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ProductMediaFileException(file.InvalidPathMessage);
            await Task.Run(() => File.Delete(file.FullPath), cancellationToken);
            return true;
        }
        catch (ProductMediaFileException) { throw; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (IOException exception)
        {
            throw new ProductMediaFileException("Lỗi server khi xóa tệp.", 500, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ProductMediaFileException("Lỗi server khi xóa tệp.", 500, exception);
        }
    }

    private static string ResolveUrlFilename(string value, string route, bool allowApiPrefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2_048 || value.Contains('\0'))
            throw new ProductMediaFileException(error);
        try
        {
            if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out Uri? uri)) throw new UriFormatException();
            if (uri.IsAbsoluteUri && uri.Scheme is not ("http" or "https")) throw new UriFormatException();
            string path = uri.IsAbsoluteUri ? uri.AbsolutePath : value.Split('?', '#')[0];
            path = Uri.UnescapeDataString(path).Replace('\\', '/');
            if (path.Contains('%') || path.Any(ch => char.IsControl(ch))) throw new UriFormatException();
            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            int index = allowApiPrefix && segments.Length == 3 && segments[0] == "api" ? 1 : 0;
            if (segments.Length - index != 2 || !string.Equals(segments[index], route, StringComparison.Ordinal))
                throw new UriFormatException();
            return segments[index + 1];
        }
        catch (Exception exception) when (exception is UriFormatException or ArgumentException)
        {
            throw new ProductMediaFileException(error);
        }
    }

    private static bool TryMatchGeneratedFilename(string filename, string prefix, IEnumerable<string> extensions)
    {
        string extension = Path.GetExtension(filename);
        if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;
        string stem = Path.GetFileNameWithoutExtension(filename);
        if (!stem.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)) return false;
        string suffix = stem[(prefix.Length + 1)..];
        return suffix.Length is >= 13 and <= 50 && suffix.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');
    }

    private static string ResolveContained(string parent, string child, string error)
    {
        string fullParent = Path.GetFullPath(parent);
        string candidate = Path.GetFullPath(Path.Combine(fullParent, child));
        string relative = Path.GetRelativePath(fullParent, candidate);
        if (relative.Length == 0 || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ProductMediaFileException(error);
        return candidate;
    }
}
