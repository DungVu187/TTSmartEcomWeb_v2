using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Middleware;

namespace TTSmartEcom.Api.Extensions;

public static class FrontendStaticFileExtensions
{
    private static readonly PathString[] ApiRoots =
    [
        "/control-plane",
        "/users",
        "/products",
        "/orders",
        "/chips",
        "/carts",
        "/manages",
        "/iporders",
        "/eporders",
        "/stations",
        "/histories",
        "/activity-logs",
        "/images",
        "/documents",
        "/section-images",
        "/invoice-images",
        "/zalo",
        "/telegram",
        "/voice-vocabs",
        "/health",
        "/socket.io",
    ];
    private static readonly HashSet<string> StaticExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".svg", ".css", ".js", ".ico", ".map",
    };

    public static WebApplication UseTtsmartFrontends(this WebApplication app)
    {
        FrontendHostingOptions options = app.Services.GetRequiredService<IOptions<FrontendHostingOptions>>().Value;
        string? customerIndex = null;
        string? adminIndex = null;

        if (options.Enabled)
        {
            customerIndex = UseBundle(app, options.CustomerDistPath, PathString.Empty);
            adminIndex = UseBundle(app, options.AdminDistPath, new PathString("/admin"));
        }

        app.MapFallback(async context =>
        {
            if (context.Request.Method is not ("GET" or "HEAD") || IsApiOrStaticRequest(context))
            {
                await WriteNotFoundAsync(context);
                return;
            }

            string? indexPath = context.Request.Path.StartsWithSegments("/admin")
                ? adminIndex
                : customerIndex;
            if (indexPath is null)
            {
                await WriteNotFoundAsync(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";
            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await context.Response.SendFileAsync(indexPath, context.RequestAborted);
            }
        });

        return app;
    }

    private static string? UseBundle(WebApplication app, string configuredPath, PathString requestPath)
    {
        string? directory = ResolveDirectory(app.Environment.ContentRootPath, configuredPath);
        if (directory is null)
        {
            return null;
        }

        string indexPath = Path.Combine(directory, "index.html");
        if (!File.Exists(indexPath))
        {
            return null;
        }

        PhysicalFileProvider provider = new(directory);
        app.Lifetime.ApplicationStopped.Register(provider.Dispose);
        app.UseWhen(
            static context => !context.Items.ContainsKey(ApiPrefixCompatibilityMiddleware.PrefixedItemKey),
            branch => branch.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = provider,
                RequestPath = requestPath,
                ServeUnknownFileTypes = false,
            }));
        return indexPath;
    }

    private static string? ResolveDirectory(string contentRoot, string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        string path = Path.IsPathFullyQualified(configuredPath)
            ? configuredPath
            : Path.Combine(contentRoot, configuredPath);
        string fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath) ? fullPath : null;
    }

    private static bool IsApiOrStaticRequest(HttpContext context)
    {
        if (context.Items.ContainsKey(ApiPrefixCompatibilityMiddleware.PrefixedItemKey) ||
            context.Request.Path.StartsWithSegments("/assets") ||
            StaticExtensions.Contains(Path.GetExtension(context.Request.Path.Value) ?? string.Empty))
        {
            return true;
        }

        return ApiRoots.Any(root => context.Request.Path == root || context.Request.Path.StartsWithSegments(root));
    }

    private static Task WriteNotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return HttpMethods.IsHead(context.Request.Method)
            ? Task.CompletedTask
            : context.Response.WriteAsJsonAsync(new { message = "Route not found" }, context.RequestAborted);
    }
}
