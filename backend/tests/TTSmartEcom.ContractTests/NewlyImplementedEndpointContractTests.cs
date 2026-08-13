using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Abstractions.Users;
using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Domain.Integrations;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.ContractTests;

public sealed class NewlyImplementedEndpointContractTests(NewEndpointWebApplicationFactory factory)
    : IClassFixture<NewEndpointWebApplicationFactory>
{
    [Fact]
    public async Task PasswordRecovery_ShouldRequestOtpThenResetWithoutExposingSecrets()
    {
        using HttpResponseMessage requestResponse = await factory.Client.PostAsJsonAsync(
            "/users/forgot-password",
            new { email = "synthetic.customer@example.test" });
        string requestBody = await requestResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);
        Assert.NotNull(factory.Users.StoredOtp);
        Assert.NotNull(factory.Email.LastMessage);
        Assert.Equal(factory.Users.StoredOtp, factory.Email.LastMessage.Otp);
        Assert.DoesNotContain(factory.Users.StoredOtp, requestBody, StringComparison.Ordinal);

        const string newPassword = "synthetic-new-password";
        using HttpResponseMessage resetResponse = await factory.Client.PostAsJsonAsync(
            "/api/users/reset-password",
            new
            {
                email = "synthetic.customer@example.test",
                otp = factory.Users.StoredOtp,
                newPassword,
            });
        string resetBody = await resetResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.Equal(factory.Users.StoredOtp, factory.Users.ResetOtp);
        Assert.Equal($"hash:{newPassword}", factory.Users.NewPasswordHash);
        Assert.Matches("^[0-9a-f]{64}$", factory.Users.ReplacementLoginToken);
        Assert.DoesNotContain(newPassword, resetBody, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.Users.StoredOtp, resetBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductImageUpload_WithEditPermission_ShouldPersistValidatedFile()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        using MultipartFormDataContent multipart = new();
        using ByteArrayContent file = new(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(file, "product", "synthetic.png");
        using HttpRequestMessage request = NewEndpointWebApplicationFactory.AuthenticatedRequest(
            HttpMethod.Post,
            "/products/upload/image",
            multipart);

        using HttpResponseMessage response = await factory.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received {(int)response.StatusCode}: {body}; auth={string.Join(';', response.Headers.WwwAuthenticate)}");
        using JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal(1, json.RootElement.GetProperty("success").GetInt32());
        Assert.Contains("/images/product_", json.RootElement.GetProperty("imgUrl").GetString(), StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(Path.Combine(factory.UploadRoot, "images"), "product_*.png"));
    }

    [Fact]
    public async Task TelegramTest_WithConfiguredAdapter_ShouldReportSentMessage()
    {
        using HttpRequestMessage request = NewEndpointWebApplicationFactory.AuthenticatedRequest(
            HttpMethod.Post,
            "/api/telegram/test",
            JsonContent.Create(new { chatId = "synthetic-chat" }));

        using HttpResponseMessage response = await factory.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received {(int)response.StatusCode}: {body}; auth={string.Join(';', response.Headers.WwwAuthenticate)}");
        using JsonDocument json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("sent").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("failed").GetInt32());
        Assert.Equal(["synthetic-chat"], factory.Telegram.ChatIds);
        Assert.DoesNotContain(NewEndpointWebApplicationFactory.TelegramToken, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZaloOAuth_ShouldIssueSignedState_ExchangeOnce_AndRedirect()
    {
        using HttpRequestMessage authRequest = NewEndpointWebApplicationFactory.AuthenticatedRequest(
            HttpMethod.Get,
            "/api/zalo/auth-url",
            null);
        using HttpResponseMessage authResponse = await factory.Client.SendAsync(authRequest);
        string authBody = await authResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
        using JsonDocument authJson = JsonDocument.Parse(authBody);
        string authUrl = authJson.RootElement.GetProperty("authUrl").GetString()!;
        Uri authorization = new(authUrl);
        string state = authorization.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Split('=', 2))
            .Where(static pair => pair.Length == 2)
            .Single(static pair => pair[0] == "state")[1];
        state = Uri.UnescapeDataString(state);
        Assert.False(string.IsNullOrWhiteSpace(state));

        using HttpResponseMessage callback = await factory.Client.GetAsync(
            $"/zalo/callback?code=synthetic-code&oa_id=oa-1&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("http://localhost:5173/admin/zalo?link=success", callback.Headers.Location?.ToString());
        Assert.Equal("synthetic-code", factory.Zalo.LastCode);
        Assert.Equal("synthetic-access", factory.ProviderSettings.ZaloAccessToken);
        Assert.DoesNotContain("synthetic-access", authBody, StringComparison.Ordinal);

        using HttpResponseMessage replay = await factory.Client.GetAsync(
            $"/zalo/callback?code=synthetic-code&oa_id=oa-1&state={Uri.EscapeDataString(state)}");
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(1, factory.Zalo.Calls);
    }
}

public sealed class NewEndpointWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string JwtSecret = "contract-test-jwt-secret-at-least-thirty-two-bytes";
    public const string TelegramToken = "synthetic-contract-test-telegram-token";
    private const string UserId = "507f1f77bcf86cd799439011";

    public NewEndpointWebApplicationFactory()
    {
        UploadRoot = Path.Combine(Path.GetTempPath(), $"ttsmart-contract-upload-{Guid.NewGuid():N}");
        Users = new FakeUserRepository();
        Email = new FakePasswordResetEmailSender();
        Telegram = new FakeTelegramMessageSender();
        Zalo = new FakeZaloOAuthClient();
        ProviderSettings = new FakeProviderSettingsRepository();
        Client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public string UploadRoot { get; }
    public HttpClient Client { get; }
    public FakeUserRepository Users { get; }
    public FakePasswordResetEmailSender Email { get; }
    public FakeTelegramMessageSender Telegram { get; }
    public FakeZaloOAuthClient Zalo { get; }
    public FakeProviderSettingsRepository ProviderSettings { get; }

    public static HttpRequestMessage AuthenticatedRequest(HttpMethod method, string path, HttpContent? content)
    {
        HttpRequestMessage request = new(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        request.Headers.Add("Origin", "http://localhost:3000");
        return request;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = JwtSecret,
                ["JWT_SECRET"] = JwtSecret,
                ["Uploads:RootPath"] = UploadRoot,
                ["ExternalServices:TelegramBotToken"] = TelegramToken,
                ["TELEGRAM_BOT_TOKEN"] = TelegramToken,
                ["ZaloOAuth:StateSecret"] = "contract-test-zalo-state-secret-at-least-32-bytes",
                ["ZaloOAuth:AuthorizationEndpoint"] = "https://oauth.example.test/permission",
                ["ZaloOAuth:TokenEndpoint"] = "https://oauth.example.test/token",
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IUserIdentityReader>();
            services.RemoveAll<IPasswordResetEmailSender>();
            services.RemoveAll<IPasswordHashWriter>();
            services.RemoveAll<IProviderSettingsRepository>();
            services.RemoveAll<ITelegramMessageSender>();
            services.RemoveAll<IProductMediaRepository>();
            services.RemoveAll<IZaloOAuthClient>();

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
            });

            services.AddSingleton<IUserRepository>(Users);
            services.AddSingleton<IUserIdentityReader>(new FakeIdentityReader());
            services.AddSingleton<IPasswordResetEmailSender>(Email);
            services.AddSingleton<IPasswordHashWriter>(new FakePasswordHashWriter());
            services.AddSingleton<IProviderSettingsRepository>(ProviderSettings);
            services.AddSingleton<ITelegramMessageSender>(Telegram);
            services.AddSingleton<IZaloOAuthClient>(Zalo);
            services.AddSingleton<IProductMediaRepository>(new FakeProductMediaRepository());
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client.Dispose();
            if (Directory.Exists(UploadRoot))
            {
                Directory.Delete(UploadRoot, recursive: true);
            }
        }

        base.Dispose(disposing);
    }

    private static string CreateToken()
    {
        DateTime now = DateTime.UtcNow;
        Claim[] claims =
        [
            new("userId", UserId),
            new("role", "admin"),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
        ];
        JwtSecurityToken token = new(
            claims: claims,
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
                ["product.create", "product.edit"],
                null));
    }

    private sealed class FakePasswordHashWriter : IPasswordHashWriter
    {
        public string Hash(string password) => $"hash:{password}";
    }

    public sealed class FakeProviderSettingsRepository : IProviderSettingsRepository
    {
        public string? ZaloAccessToken { get; private set; }
        public Task<TelegramSettings> GetTelegramAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new TelegramSettings(true, []));

        public Task<TelegramSettings> SetTelegramEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
            Task.FromResult(new TelegramSettings(enabled, []));

        public Task<TelegramRecipient> AddTelegramRecipientAsync(TelegramRecipientInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new TelegramRecipient("recipient", input.Label ?? "", input.ChatId ?? "", input.Type ?? "personal", input.Enabled ?? true, input.NotifyTypes ?? []));

        public Task<TelegramRecipient?> UpdateTelegramRecipientAsync(string id, TelegramRecipientInput input, CancellationToken cancellationToken) =>
            Task.FromResult<TelegramRecipient?>(null);

        public Task<bool> DeleteTelegramRecipientAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<ZaloSettings> GetZaloAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ZaloSettings("app-1", "", "", false, null, true));

        public Task<ZaloSettings> UpdateZaloAsync(ZaloSettingsInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new ZaloSettings("", "", "", false, null, false));

        public Task<string?> GetZaloSecretKeyAsync(CancellationToken cancellationToken) => Task.FromResult<string?>("secret-1");

        public Task SaveZaloOAuthTokensAsync(string accessToken, string? refreshToken, DateTimeOffset expiresAt, string? oaId, CancellationToken cancellationToken)
        {
            ZaloAccessToken = accessToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProductMediaRepository : IProductMediaRepository
    {
        public Task<ProductVariantImageReference?> GetVariantImageReferenceAsync(string productId, int variantIndex, CancellationToken cancellationToken) =>
            Task.FromResult<ProductVariantImageReference?>(null);

        public Task<bool> IsProductImageReferencedElsewhereAsync(string productId, int variantIndex, string filename, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<ProductRecord?> ClearVariantImageAsync(string productId, int variantIndex, string expectedImageUrl, CancellationToken cancellationToken) =>
            Task.FromResult<ProductRecord?>(null);

        public Task<bool> IsInvoiceImageReferencedAsync(string filename, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}

public sealed class FakeZaloOAuthClient : IZaloOAuthClient
{
    public int Calls { get; private set; }
    public string? LastCode { get; private set; }

    public string? BuildAuthorizationUrl(string appId, string redirectUri, string state) =>
        $"https://oauth.example.test/permission?app_id={Uri.EscapeDataString(appId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}";

    public Task<ZaloOAuthTokenExchangeResult> ExchangeCodeAsync(string appId, string secretKey, string code, CancellationToken cancellationToken)
    {
        Calls++;
        LastCode = code;
        return Task.FromResult(new ZaloOAuthTokenExchangeResult(
            ZaloOAuthTokenExchangeStatus.Success,
            "synthetic-access",
            "synthetic-refresh",
            3600));
    }
}

public sealed class FakeUserRepository : IUserRepository
{
    public string? StoredOtp { get; private set; }
    public string? ResetOtp { get; private set; }
    public string? NewPasswordHash { get; private set; }
    public string? ReplacementLoginToken { get; private set; }

    public Task<UserRecord?> FindByLoginAsync(string identifier, CancellationToken cancellationToken) =>
        Task.FromResult<UserRecord?>(null);

    public Task<PasswordRecoveryUser?> FindForPasswordRecoveryAsync(string identifier, CancellationToken cancellationToken) =>
        Task.FromResult<PasswordRecoveryUser?>(new PasswordRecoveryUser(
            "507f1f77bcf86cd799439011",
            "0900000000",
            "synthetic.customer@example.test",
            "Synthetic Customer"));

    public Task<bool> StorePasswordResetOtpAsync(string userId, string otp, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        StoredOtp = otp;
        return Task.FromResult(true);
    }

    public Task<bool> ClearPasswordResetOtpAsync(string userId, string expectedOtp, CancellationToken cancellationToken)
    {
        if (StoredOtp == expectedOtp)
        {
            StoredOtp = null;
        }

        return Task.FromResult(true);
    }

    public Task<bool> ResetPasswordWithOtpAsync(
        string userId,
        string expectedOtp,
        DateTimeOffset now,
        string passwordHash,
        string replacementLoginToken,
        DateTimeOffset passwordChangedAt,
        CancellationToken cancellationToken)
    {
        ResetOtp = expectedOtp;
        NewPasswordHash = passwordHash;
        ReplacementLoginToken = replacementLoginToken;
        return Task.FromResult(expectedOtp == StoredOtp);
    }

    public Task<UserRecord?> ConsumeAutologinTokenAsync(string token, string replacementToken, CancellationToken cancellationToken) =>
        Task.FromResult<UserRecord?>(null);

    public Task<UserIdentitySnapshot?> FindIdentityAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<UserIdentitySnapshot?>(null);
}

public sealed class FakePasswordResetEmailSender : IPasswordResetEmailSender
{
    public PasswordResetEmailMessage? LastMessage { get; private set; }

    public Task<PasswordResetEmailDeliveryStatus> SendAsync(PasswordResetEmailMessage message, CancellationToken cancellationToken)
    {
        LastMessage = message;
        return Task.FromResult(PasswordResetEmailDeliveryStatus.Delivered);
    }
}

public sealed class FakeTelegramMessageSender : ITelegramMessageSender
{
    private readonly List<string> chatIds = [];
    public IReadOnlyList<string> ChatIds => chatIds;

    public Task<bool> SendAsync(string chatId, string message, CancellationToken cancellationToken)
    {
        chatIds.Add(chatId);
        return Task.FromResult(true);
    }
}
