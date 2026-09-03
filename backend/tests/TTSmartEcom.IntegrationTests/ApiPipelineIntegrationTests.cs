using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TTSmartEcom.IntegrationTests;

public sealed class ApiPipelineIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public ApiPipelineIntegrationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/api/health/live")]
    public async Task HealthPipeline_ShouldPreserveCorrelationId(string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-ID", "integration-correlation-01");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("integration-correlation-01", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task FrontendPipeline_ShouldServeBundlesAndSpaFallbacksWithoutCapturingApiPaths()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ttsmart-frontends-{Guid.NewGuid():N}");
        string customer = Path.Combine(root, "customer");
        string admin = Path.Combine(root, "admin");
        Directory.CreateDirectory(Path.Combine(customer, "assets"));
        Directory.CreateDirectory(Path.Combine(admin, "assets"));
        await File.WriteAllTextAsync(Path.Combine(customer, "index.html"), "<html>customer-shell</html>");
        await File.WriteAllTextAsync(Path.Combine(admin, "index.html"), "<html>admin-shell</html>");
        await File.WriteAllTextAsync(Path.Combine(customer, "assets", "customer.js"), "customer-asset");
        await File.WriteAllTextAsync(Path.Combine(admin, "assets", "admin.js"), "admin-asset");

        try
        {
            await using WebApplicationFactory<Program> configuredFactory = factory.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["FrontendHosting:Enabled"] = "true",
                        ["FrontendHosting:CustomerDistPath"] = customer,
                        ["FrontendHosting:AdminDistPath"] = admin,
                    })));
            using HttpClient configuredClient = configuredFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            using HttpResponseMessage customerFallback = await configuredClient.GetAsync("/catalog/deep-link");
            using HttpResponseMessage adminFallback = await configuredClient.GetAsync("/admin/orders/deep-link");
            using HttpResponseMessage customerAsset = await configuredClient.GetAsync("/assets/customer.js");
            using HttpResponseMessage adminAsset = await configuredClient.GetAsync("/admin/assets/admin.js");
            using HttpResponseMessage apiMiss = await configuredClient.GetAsync("/api/not-a-route");
            using HttpResponseMessage controlPlaneMiss = await configuredClient.GetAsync("/control-plane/not-a-route");
            using HttpResponseMessage prefixedAssetMiss = await configuredClient.GetAsync("/api/assets/customer.js");
            using HttpResponseMessage staticMiss = await configuredClient.GetAsync("/assets/missing.js");

            Assert.Equal("<html>customer-shell</html>", await customerFallback.Content.ReadAsStringAsync());
            Assert.Equal("<html>admin-shell</html>", await adminFallback.Content.ReadAsStringAsync());
            Assert.Contains("no-store", customerFallback.Headers.CacheControl?.ToString());
            Assert.Equal("customer-asset", await customerAsset.Content.ReadAsStringAsync());
            Assert.Equal("admin-asset", await adminAsset.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, apiMiss.StatusCode);
            Assert.Equal("{\"message\":\"Route not found\"}", await apiMiss.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, controlPlaneMiss.StatusCode);
            Assert.Equal("{\"message\":\"Route not found\"}", await controlPlaneMiss.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, prefixedAssetMiss.StatusCode);
            Assert.NotEqual("customer-asset", await prefixedAssetMiss.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, staticMiss.StatusCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReverseProxyConfiguration_UsesOnlyConfiguredForwarders()
    {
        using WebApplicationFactory<Program> configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ReverseProxy:Enabled"] = "true",
                    ["ReverseProxy:ForwardLimit"] = "2",
                    ["ReverseProxy:KnownProxies:0"] = "10.0.0.10",
                    ["ReverseProxy:KnownNetworks:0"] = "192.0.2.0/24",
                })));

        ForwardedHeadersOptions options = configuredFactory.Services
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(2, options.ForwardLimit);
        Assert.Single(options.KnownProxies);
        Assert.Equal(IPAddress.Parse("10.0.0.10"), options.KnownProxies[0]);
        Assert.Single(options.KnownIPNetworks);
        Assert.Equal(System.Net.IPNetwork.Parse("192.0.2.0/24"), options.KnownIPNetworks[0]);
    }
}
