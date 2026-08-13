using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TTSmartEcom.SecurityTests;

public sealed class NewEndpointBoundarySecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public NewEndpointBoundarySecurityTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Theory]
    [InlineData("/products/upload/image", "product", "synthetic.png", "image/png")]
    [InlineData("/products/upload/document", "document", "synthetic.pdf", "application/pdf")]
    [InlineData("/chips/upload-section-image", "sectionImage", "synthetic.png", "image/png")]
    [InlineData("/api/products/upload/image", "product", "synthetic.png", "image/png")]
    public async Task NewlyImplementedUpload_WithoutSession_ShouldRejectBeforeFileHandling(
        string path,
        string field,
        string filename,
        string contentType)
    {
        using MultipartFormDataContent multipart = new();
        using ByteArrayContent file = new(Encoding.UTF8.GetBytes("synthetic-not-a-real-file"));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(file, field, filename);

        using HttpResponseMessage response = await client.PostAsync(path, multipart);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/telegram/test")]
    [InlineData("/api/telegram/test")]
    public async Task TelegramTest_WithoutAdminSession_ShouldRejectBeforeProvider(string path)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(path, new { chatId = "synthetic-chat" });
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("TELEGRAM_BOT_TOKEN", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("synthetic-chat", body, StringComparison.Ordinal);
    }
}
