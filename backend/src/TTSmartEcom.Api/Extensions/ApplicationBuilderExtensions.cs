using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Realtime;

namespace TTSmartEcom.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseTtsmartPipeline(this WebApplication app)
    {
        CorsOptions cors = app.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
        string[] origins = cors.AllowedOrigins.Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)).ToArray();

        app.UseTtsmartReverseProxy();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<LegacyExceptionMiddleware>();
        app.UseMiddleware<ApiPrefixCompatibilityMiddleware>();
        app.UseTtsmartSocketIoRealtime();
        app.UseRouting();
        app.UseMiddleware<LegacyCsrfOriginMiddleware>();
        app.UseCors(policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
        });
        app.UseRateLimiter();
        app.UseStaticFiles();
        app.UseTtsmartPublicUploadFiles();
        app.UseAuthentication();
        app.UseMiddleware<CurrentUserContextMiddleware>();
        app.UseMiddleware<LegacyPrincipalMiddleware>();
        app.UseAuthorization();
        app.UseTtsmartProtectedInvoiceFiles();
        app.MapControllers();
        app.MapTtsmartSocketIoRealtime();

        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/health/ready", async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
        {
            HealthReport report = await healthChecks.CheckHealthAsync(cancellationToken);
            return report.Status == HealthStatus.Healthy
                ? Results.Ok(new { status = "ok" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        });
        app.UseTtsmartFrontends();

        return app;
    }
}
