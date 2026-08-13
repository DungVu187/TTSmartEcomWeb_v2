using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TTSmartEcom.SecurityTests;

public sealed class ApiBoundarySecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public ApiBoundarySecurityTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Theory]
    [InlineData("/users/profile")]
    [InlineData("/carts/getCart")]
    [InlineData("/activity-logs")]
    [InlineData("/api/carts/getCart")]
    public async Task ProtectedRoute_WithoutToken_ShouldReject(string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithMalformedCookie_ShouldRejectWithoutLeakingDetails()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/users/profile");
        request.Headers.Add("Cookie", "authToken=not-a-jwt");

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnsafeRequest_FromUntrustedOrigin_ShouldBeRejectedBeforeBusinessLogic()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/users/login")
        {
            Content = JsonContent.Create(new { identifier = "synthetic", password = "synthetic" }),
        };
        request.Headers.Add("Origin", "https://untrusted.invalid");
        request.Headers.Add("Cookie", "authToken=synthetic-cookie");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CookieAuthenticatedMutation_WithoutBrowserProvenance_ShouldBeRejected()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/users/logout")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Add("Cookie", "authToken=synthetic-cookie");

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("TTS-CSRF-0001", body);
    }

    [Fact]
    public async Task CookieAuthenticatedMutation_FromAllowedOrigin_ShouldReachAuthentication()
    {
        using HttpRequestMessage request = new(HttpMethod.Put, "/users/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = "synthetic", newPassword = "synthetic-new" }),
        };
        request.Headers.Add("Cookie", "authToken=synthetic-cookie");
        request.Headers.Add("Origin", "http://localhost:3000");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CookieAuthenticatedMutation_FromSameSiteSiblingWithoutOrigin_ShouldBeRejected()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/users/logout");
        request.Headers.Add("Cookie", "authToken=synthetic.invalid.token");
        request.Headers.Add("Sec-Fetch-Site", "same-site");

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("TTS-CSRF-0001", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CookieAuthenticatedMutation_FromSameOriginFetchMetadata_ShouldReachAuthentication()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/users/logout");
        request.Headers.Add("Cookie", "authToken=synthetic.invalid.token");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_ShouldNotLeakStackOrConfiguration()
    {
        using HttpResponseMessage response = await client.GetAsync("/api/definitely-not-an-endpoint");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionstring", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Liveness_ShouldExposeOnlyStableStatus()
    {
        using HttpResponseMessage response = await client.GetAsync("/health/live");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", body);
        Assert.DoesNotContain("database", body, StringComparison.OrdinalIgnoreCase);
    }
}
