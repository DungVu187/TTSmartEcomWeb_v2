using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.ContractTests;

public sealed class ProductReadProjectionEndpointContractTests(ProductReadProjectionWebApplicationFactory factory)
    : IClassFixture<ProductReadProjectionWebApplicationFactory>
{
    [Fact]
    public async Task PublicDetail_WithPrivilegedCookie_OmitsPrivateVariantFields()
    {
        using HttpRequestMessage request = ProductReadProjectionWebApplicationFactory.Authenticated(
            HttpMethod.Get,
            $"/products/{ProductReadProjectionWebApplicationFactory.VisibleProductId}");

        using HttpResponseMessage response = await factory.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement variant = document.RootElement.GetProperty("variant")[0];
        Assert.False(variant.TryGetProperty("importPrice", out _));
        Assert.False(variant.TryGetProperty("earn", out _));
    }

    [Fact]
    public async Task PublicFetchByIds_WithPrivilegedCookie_OmitsPrivateVariantFields()
    {
        using HttpRequestMessage request = ProductReadProjectionWebApplicationFactory.Authenticated(
            HttpMethod.Post,
            "/api/products/fetch-by-ids",
            JsonContent.Create(new { ids = new[] { ProductReadProjectionWebApplicationFactory.VisibleProductId } }));
        request.Headers.Add("Origin", "http://localhost:3000");

        using HttpResponseMessage response = await factory.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement variant = document.RootElement.GetProperty("products")[0].GetProperty("variant")[0];
        Assert.False(variant.TryGetProperty("importPrice", out _));
        Assert.False(variant.TryGetProperty("earn", out _));
    }

    [Fact]
    public async Task AdminDetail_ReturnsPrivateVariantFieldsForHiddenProduct()
    {
        using HttpRequestMessage request = ProductReadProjectionWebApplicationFactory.Authenticated(
            HttpMethod.Get,
            $"/products/{ProductReadProjectionWebApplicationFactory.HiddenProductId}/admin-detail");

        using HttpResponseMessage response = await factory.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("display").GetBoolean());
        JsonElement variant = root.GetProperty("variant")[0];
        Assert.Equal("80", variant.GetProperty("importPrice").GetString());
        Assert.Equal(25, variant.GetProperty("earn").GetDouble());
    }
}

public sealed class ProductReadProjectionWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string JwtSecret = "product-read-contract-jwt-secret-at-least-thirty-two-bytes";
    private const string UserId = "507f1f77bcf86cd799439011";
    public const string VisibleProductId = "507f1f77bcf86cd799439012";
    public const string HiddenProductId = "507f1f77bcf86cd799439013";

    public ProductReadProjectionWebApplicationFactory()
    {
        Client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    public HttpClient Client { get; }

    public static HttpRequestMessage Authenticated(HttpMethod method, string path, HttpContent? content = null)
    {
        HttpRequestMessage request = new(method, path) { Content = content };
        request.Headers.Add("Cookie", $"authToken={CreateToken()}");
        return request;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = JwtSecret,
                ["JWT_SECRET"] = JwtSecret,
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserIdentityReader>();
            services.RemoveAll<IProductCatalogRepository>();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)));
            services.AddSingleton<IUserIdentityReader>(new FakeIdentityReader());
            services.AddSingleton<IProductCatalogRepository>(new FakeProductRepository());
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Client.Dispose();
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
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeIdentityReader : IUserIdentityReader
    {
        public Task<UserIdentitySnapshot?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserIdentitySnapshot?>(new UserIdentitySnapshot(
                UserId,
                "admin@example.test",
                "0900000000",
                "Synthetic Admin",
                "admin",
                [],
                ["product.edit"],
                null));
    }

    private sealed class FakeProductRepository : IProductCatalogRepository
    {
        public Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ProductPage(0, query.Page, query.Limit, []));

        public Task<ProductRecord?> FindByIdAsync(
            string id,
            bool includePrivate,
            CancellationToken cancellationToken)
        {
            if (id == HiddenProductId && !includePrivate) return Task.FromResult<ProductRecord?>(null);
            if (id is not (VisibleProductId or HiddenProductId)) return Task.FromResult<ProductRecord?>(null);
            return Task.FromResult<ProductRecord?>(Product(id, id == VisibleProductId, includePrivate));
        }

        public Task<IReadOnlyList<ProductRecord>> FindByIdsAsync(
            IReadOnlyCollection<string> ids,
            bool includePrivate,
            CancellationToken cancellationToken)
        {
            ProductRecord[] products = ids
                .Where(id => id == VisibleProductId || includePrivate && id == HiddenProductId)
                .Select(id => Product(id, id == VisibleProductId, includePrivate))
                .ToArray();
            return Task.FromResult<IReadOnlyList<ProductRecord>>(products);
        }

        public Task<IReadOnlyList<ProductTypeRecord>> ListTypesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProductTypeRecord>>([]);

        private static ProductRecord Product(string id, bool display, bool includePrivate) => new(
            id,
            "PLC",
            display ? "Sản phẩm hiển thị" : "Sản phẩm ẩn",
            null,
            display,
            display ? "VISIBLE-001" : "HIDDEN-001",
            "10",
            true,
            "Siemens",
            "Điều khiển",
            null,
            [new ProductVariant(
                "507f1f77bcf86cd799439014",
                "100",
                includePrivate ? "80" : null,
                includePrivate ? 25 : null,
                null,
                null,
                null,
                null,
                null,
                1,
                2,
                null)],
            null,
            [],
            0,
            [],
            0,
            0,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true);
    }
}
