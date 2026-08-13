namespace TTSmartEcom.Api.Configuration;

public static class UploadPathResolver
{
    public static string ResolveRoot(UploadOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        string configuredPath = options.RootPath.Trim();
        if (configuredPath.Length == 0)
        {
            throw new InvalidOperationException("Uploads:RootPath must not be empty.");
        }
        string candidate = Path.IsPathFullyQualified(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
        return Path.GetFullPath(candidate);
    }

    public static string ResolveSubdirectory(UploadOptions options, IHostEnvironment environment, string directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName)
            || directoryName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            || directoryName is "." or "..")
        {
            throw new ArgumentException("Upload directory name is invalid.", nameof(directoryName));
        }

        string root = ResolveRoot(options, environment);
        string directory = Path.GetFullPath(Path.Combine(root, directoryName));
        string relative = Path.GetRelativePath(root, directory);
        if (Path.IsPathFullyQualified(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Upload directory escapes the configured root.");
        }

        return directory;
    }
}
