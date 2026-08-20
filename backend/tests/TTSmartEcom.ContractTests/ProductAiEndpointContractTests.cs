using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Catalog;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.ContractTests;

public sealed class ProductAiEndpointContractTests(ProductAiWebApplicationFactory factory)
    : IClassFixture<ProductAiWebApplicationFactory>
{
    [Fact]
    public async Task ScanInvoice_WithFakeProvider_ShouldReturnItemsAndStoredImage()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        using MultipartFormDataContent multipart = Multipart("invoice", "synthetic.png", "image/png", png);
        using HttpRequestMessage request = ProductAiWebApplicationFactory.Authenticated(
            HttpMethod.Post, "/products/scan-invoice", multipart);

        using HttpResponseMessage response = await factory.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal(1, json.RootElement.GetProperty("success").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("PLC S7-1200", json.RootElement.GetProperty("items")[0].GetProperty("rawScannedName").GetString());
        Assert.StartsWith("/invoice-images/invoice-scan-", json.RootElement.GetProperty("imageUrl").GetString(), StringComparison.Ordinal);
        Assert.Equal("image/png", factory.Provider.InvoiceContentType);
        Assert.Equal(png, factory.Provider.InvoiceContent);
    }

    [Fact]
    public async Task VoiceAudio_WithFakeProvider_ShouldNormalizeResult()
    {
        byte[] audio = Encoding.UTF8.GetBytes("synthetic audio");
        using MultipartFormDataContent multipart = Multipart("audio", "query.webm", "audio/webm", audio);
        using HttpRequestMessage request = ProductAiWebApplicationFactory.Authenticated(
            HttpMethod.Post, "/api/products/voice-query", multipart);

        using HttpResponseMessage response = await factory.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal("PLC", json.RootElement.GetProperty("keyword").GetString());
        Assert.Equal("Siemens", json.RootElement.GetProperty("filters").GetProperty("brand").GetString());
        Assert.Equal(audio, factory.Provider.VoiceContent);
    }

    [Fact]
    public async Task VoiceText_ShouldNotCallProvider()
    {
        using HttpRequestMessage request = ProductAiWebApplicationFactory.Authenticated(
            HttpMethod.Post,
            "/products/voice-query-text",
            JsonContent.Create(new { text = "tìm plc siemens nhé" }));

        using HttpResponseMessage response = await factory.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, factory.Provider.VoiceCalls);
        using JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal("PLC", json.RootElement.GetProperty("keyword").GetString());
        Assert.Equal("Siemens", json.RootElement.GetProperty("filters").GetProperty("brand").GetString());
    }

    [Fact]
    public async Task AudioRoutes_WhenProviderIsNotConfigured_ShouldFailClosedBeforeReadingFile()
    {
        factory.Provider.Configured = false;
        try
        {
            using MultipartFormDataContent multipart = Multipart(
                "audio", "query.webm", "audio/webm", Encoding.UTF8.GetBytes("synthetic audio"));
            using HttpRequestMessage request = ProductAiWebApplicationFactory.Authenticated(
                HttpMethod.Post, "/products/voice-query", multipart);

            using HttpResponseMessage response = await factory.Client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("GEMINI_API_KEY", body, StringComparison.Ordinal);
            Assert.Equal(0, factory.Provider.VoiceCalls);
        }
        finally
        {
            factory.Provider.Configured = true;
        }
    }

    private static MultipartFormDataContent Multipart(string field, string filename, string contentType, byte[] content)
    {
        MultipartFormDataContent multipart = new();
        ByteArrayContent file = new(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(file, field, filename);
        return multipart;
    }
}

public sealed class ProductAiWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string JwtSecret = "product-ai-contract-jwt-secret-at-least-thirty-two-bytes";
    private const string UserId = "507f1f77bcf86cd799439011";

    public ProductAiWebApplicationFactory()
    {
        UploadRoot = Path.Combine(Path.GetTempPath(), $"ttsmart-ai-upload-{Guid.NewGuid():N}");
        Provider = new FakeProductAiProvider();
        Client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public string UploadRoot { get; }
    public FakeProductAiProvider Provider { get; }
    public HttpClient Client { get; }

    public static HttpRequestMessage Authenticated(HttpMethod method, string path, HttpContent content)
    {
        HttpRequestMessage request = new(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        request.Headers.Add("Origin", "http://localhost:3000");
        return request;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Uploads:RootPath"] = UploadRoot,
                ["Uploads:RecordMetadata"] = "false",
                ["Jwt:Secret"] = JwtSecret,
                ["JWT_SECRET"] = JwtSecret,
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProductAiProvider>();
            services.RemoveAll<IUserIdentityReader>();
            services.RemoveAll<IProductCatalogRepository>();
            services.RemoveAll<ICatalogRepository>();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)));
            services.AddSingleton<IProductAiProvider>(Provider);
            services.AddSingleton<IUserIdentityReader>(new FakeIdentityReader());
            services.AddSingleton<IProductCatalogRepository>(new FakeCatalogProducts());
            services.AddSingleton<ICatalogRepository>(new FakeCatalog());
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client.Dispose();
            if (Directory.Exists(UploadRoot)) Directory.Delete(UploadRoot, recursive: true);
        }
        base.Dispose(disposing);
    }

    private static string CreateToken()
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            claims:
            [
                new Claim("userId", UserId),
                new Claim("role", "admin"),
                new Claim(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeIdentityReader : IUserIdentityReader
    {
        public Task<UserIdentitySnapshot?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserIdentitySnapshot?>(new UserIdentitySnapshot(
                UserId, "admin@example.test", "0900000000", "Synthetic Admin", "admin", [],
                ["order.scan_ai", "product.view"], null));
    }

    private sealed class FakeCatalogProducts : IProductCatalogRepository
    {
        private static readonly ProductRecord Product = new(
            "507f1f77bcf86cd799439012", "PLC", "PLC S7-1200", null, true, "S7-1200", "10%", true,
            "Siemens", "Điều khiển", null, [], null, [], 0, [], 0, 0, 0, null, null, null, null, null,
            null, null, null, null, true);

        public Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ProductPage(1, 1, 10_000, [Product]));
        public Task<ProductRecord?> FindByIdAsync(string id, bool includePrivate, CancellationToken cancellationToken) => Task.FromResult<ProductRecord?>(Product);
        public Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(IReadOnlyCollection<string> ids, bool includePrivate, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductRecord>>([Product]);
        public Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductTypeRecord>>([]);
    }

    private sealed class FakeCatalog : ICatalogRepository
    {
        public Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandRecord>>([new BrandRecord("brand-1", "Siemens")]);
        public Task<IReadOnlyList<string>> ListSectionNamesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<SectionDocumentRecord?> GetSectionDocumentAsync(CancellationToken cancellationToken) => Task.FromResult<SectionDocumentRecord?>(null);
        public Task<IReadOnlyList<string>?> GetSectionValuesAsync(string sectionName, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>?>(null);
        public Task<IReadOnlyDictionary<string, string?>> GetSectionImagesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
        public Task<ManageRecord?> GetManageAsync(CancellationToken cancellationToken) => Task.FromResult<ManageRecord?>(null);
        public Task<IReadOnlyList<ManagePolicyRecord>> GetPoliciesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ManagePolicyRecord>>([]);
    }
}

public sealed class FakeProductAiProvider : IProductAiProvider
{
    public bool Configured { get; set; } = true;
    public bool IsConfigured => Configured;
    public string? InvoiceContentType { get; private set; }
    public byte[]? InvoiceContent { get; private set; }
    public byte[]? VoiceContent { get; private set; }
    public int VoiceCalls { get; private set; }

    public Task<ProductAiResult> AnalyzeInvoiceAsync(string contentType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        InvoiceContentType = contentType;
        InvoiceContent = content.ToArray();
        using JsonDocument json = JsonDocument.Parse("""
            [{"stt":"1","rawScannedName":"PLC S7-1200","code":"S7-1200","brand":"Siemens","quantity":1,"price":1000}]
            """);
        return Task.FromResult(ProductAiResult.Success(json.RootElement));
    }

    public Task<ProductAiResult> AnalyzeVoiceAsync(string contentType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        VoiceCalls++;
        VoiceContent = content.ToArray();
        using JsonDocument json = JsonDocument.Parse("""
            {"transcript":"tìm plc siemens","keyword":"PLC Siemens","intent":"search_product","filters":{"brand":"Siemens","type":"PLC","code":null}}
            """);
        return Task.FromResult(ProductAiResult.Success(json.RootElement));
    }
}
