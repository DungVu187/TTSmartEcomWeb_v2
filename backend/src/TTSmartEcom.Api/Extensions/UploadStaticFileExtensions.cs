using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;

namespace TTSmartEcom.Api.Extensions;

public static class UploadStaticFileExtensions
{
    public static IApplicationBuilder UseTtsmartPublicUploadFiles(this WebApplication app)
    {
        app.UseUploadDirectory("images", "/images");
        app.UseUploadDirectory("documents", "/documents");
        app.UseUploadDirectory("sections", "/section-images");
        app.UseUploadDirectory("stations", "/station");
        return app;
    }

    public static IApplicationBuilder UseTtsmartProtectedInvoiceFiles(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/invoice-images"))
            {
                await next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Access denied, no token provided" });
                return;
            }

            UserIdentitySnapshot? identity = context.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
            if (identity is null || identity.Role is not ("superadmin" or "admin" or "staff"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Access denied, not an admin or staff" });
                return;
            }

            await next(context);
        });
        app.UseUploadDirectory("invoices", "/invoice-images");
        return app;
    }

    private static void UseUploadDirectory(this WebApplication app, string directoryName, string requestPath)
    {
        UploadOptions options = app.Services.GetRequiredService<IOptions<UploadOptions>>().Value;
        string directory = UploadPathResolver.ResolveSubdirectory(options, app.Environment, directoryName);
        Directory.CreateDirectory(directory);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(directory),
            RequestPath = requestPath,
            ServeUnknownFileTypes = false,
        });
    }
}
