using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TTSmartEcom.SecurityTests;

public sealed class ZaloOAuthBoundarySecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public ZaloOAuthBoundarySecurityTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Theory]
    [InlineData("/zalo/auth-url")]
    [InlineData("/api/zalo/auth-url")]
    public async Task AuthUrl_WithoutAdminSession_ShouldRejectBeforeStateCreation(string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("state", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/zalo/callback?code=synthetic&state=forged")]
    [InlineData("/api/zalo/callback?code=synthetic&state=forged")]
    public async Task Callback_WithForgedState_ShouldFailBeforeProvider(string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.ServiceUnavailable);
        Assert.DoesNotContain("synthetic", body, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_key", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Callback_WithMissingCode_ShouldReturnBoundedValidationMessage()
    {
        using HttpResponseMessage response = await client.GetAsync("/zalo/callback?state=synthetic");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb", body, StringComparison.OrdinalIgnoreCase);
    }
}
