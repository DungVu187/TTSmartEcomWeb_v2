using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace TTSmartEcom.ContractTests;

public sealed class ApiContractSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public ApiContractSmokeTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task LiveHealth_ShouldReturnLegacySafeShapeAndCorrelationId()
    {
        using HttpResponseMessage response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out IEnumerable<string>? values));
        Assert.False(string.IsNullOrWhiteSpace(values.Single()));
        Assert.Contains("\"status\":\"ok\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiPrefixedLogin_ShouldUseSameCompatibilityAlias()
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/users/login", new
        {
            identifier = "",
            password = "",
        });

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedProfile_ShouldReturnSafeUnauthorizedWithoutStackTrace()
    {
        using HttpResponseMessage response = await client.GetAsync("/users/profile");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionstring", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/users/forgot-password")]
    [InlineData("/api/users/forgot-password")]
    public async Task ForgotPassword_WithMissingIdentifier_ShouldPreserveLegacyValidation(string path)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(path, new { });
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Vui lòng cung cấp số điện thoại hoặc email", body);
    }

    [Theory]
    [InlineData("/users/reset-password")]
    [InlineData("/api/users/reset-password")]
    public async Task ResetPassword_WithMissingFields_ShouldPreserveLegacyValidation(string path)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(path, new { });
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Vui lòng nhập đầy đủ thông tin yêu cầu", body);
    }

    [Fact]
    public async Task Register_WithoutPublicSignupOrSession_ShouldRejectAuthentication()
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/users/register", new
        {
            phone = "0900000000",
            password = "synthetic-password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Autologin_WithUnknownToken_ShouldReturnUnauthorized()
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/users/autologin", new
        {
            token = "synthetic-token",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_WithMissingInput_ShouldExistAndRejectPayload()
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/users/admin/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PermissionCatalog_WithoutSession_ShouldReject()
    {
        using HttpResponseMessage response = await client.GetAsync("/users/permission-catalog");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "/manages/update")]
    [InlineData("POST", "/manages/update-images")]
    [InlineData("POST", "/manages/update-partners")]
    [InlineData("POST", "/manages/upload-section-image")]
    public async Task StorefrontFileRoute_WithoutSession_ShouldRejectBeforeDeferredHandler(string method, string path)
    {
        using MultipartFormDataContent multipart = new();
        using ByteArrayContent content = new(Encoding.UTF8.GetBytes("synthetic"));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");
        multipart.Add(content, path.EndsWith("upload-section-image", StringComparison.Ordinal) ? "image" : "manage", "synthetic.webp");
        using HttpRequestMessage request = new(new HttpMethod(method), path) { Content = multipart };
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/orders")]
    [InlineData("/iporders/orders")]
    [InlineData("/eporders/orders")]
    [InlineData("/telegram/settings")]
    [InlineData("/zalo/settings")]
    [InlineData("/voice-vocabs")]
    [InlineData("/histories")]
    public async Task AdministrativeRoute_WithoutSession_ShouldReject(string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/stations/507f1f77bcf86cd799439011/upload-image")]
    [InlineData("DELETE", "/stations/507f1f77bcf86cd799439011/remove-image")]
    [InlineData("POST", "/orders/upload-image")]
    [InlineData("DELETE", "/orders/delete-image?imageUrl=%2Finvoice-images%2Fsynthetic.webp")]
    [InlineData("POST", "/iporders/upload-image")]
    [InlineData("DELETE", "/iporders/delete-image?imageUrl=%2Finvoice-images%2Fsynthetic.webp")]
    [InlineData("POST", "/eporders/upload-image")]
    [InlineData("DELETE", "/eporders/delete-image?imageUrl=%2Finvoice-images%2Fsynthetic.webp")]
    public async Task AdministrativeMediaRoute_WithoutSession_ShouldRejectBeforeFileHandling(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        if (method == "POST")
        {
            using MultipartFormDataContent multipart = new();
            using ByteArrayContent content = new(Encoding.UTF8.GetBytes("synthetic"));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");
            string field = path.StartsWith("/stations/", StringComparison.Ordinal) ? "station" : "invoice";
            multipart.Add(content, field, "synthetic.webp");
            request.Content = multipart;
            using HttpResponseMessage response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            return;
        }

        using HttpResponseMessage deleteResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedInvoiceFile_WithoutSession_ShouldReject()
    {
        using HttpResponseMessage response = await client.GetAsync("/invoice-images/synthetic.webp");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ZaloCallback_WithoutSignedState_ShouldRejectBeforeProviderExchange()
    {
        using HttpResponseMessage response = await client.GetAsync("/zalo/callback?code=synthetic");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("synthetic", body, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", body, StringComparison.OrdinalIgnoreCase);
    }
}
