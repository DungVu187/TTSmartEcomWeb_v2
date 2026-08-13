using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Application.Voice;
using TTSmartEcom.Domain.Integrations;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.ContractTests;

public sealed class IntegrationActivityLogContractTests(
    IntegrationActivityLogWebApplicationFactory factory) :
    IClassFixture<IntegrationActivityLogWebApplicationFactory>
{
    private static readonly string[] NewOrderNotification = ["new_order"];

    [Fact]
    public async Task ProviderAndVoiceMutations_ShouldAppendEightLegacyActionsAfterCommit()
    {
        factory.Reset();
        using HttpResponseMessage zalo = await SendAsync(
            HttpMethod.Post,
            "/api/zalo/settings",
            new
            {
                appId = "safe-app-id",
                secretKey = IntegrationActivityLogWebApplicationFactory.SensitiveSecret,
                oaId = "safe-oa-id",
                recipientUserId = "safe-recipient-id",
            });
        Assert.Equal(HttpStatusCode.OK, zalo.StatusCode);

        using HttpResponseMessage toggle = await SendAsync(
            HttpMethod.Put,
            "/telegram/settings",
            new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);

        using HttpResponseMessage added = await SendAsync(
            HttpMethod.Post,
            "/api/telegram/recipients",
            new
            {
                label = "Nhóm vận hành",
                chatId = IntegrationActivityLogWebApplicationFactory.SensitiveChatId,
                type = "group",
                enabled = true,
                notifyTypes = NewOrderNotification,
            });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        using HttpResponseMessage updated = await SendAsync(
            HttpMethod.Put,
            $"/telegram/recipients/{IntegrationActivityLogWebApplicationFactory.RecipientId}",
            new { label = "Nhóm mới" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        using HttpResponseMessage deleted = await SendAsync(
            HttpMethod.Delete,
            $"/api/telegram/recipients/{IntegrationActivityLogWebApplicationFactory.RecipientId}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        using HttpResponseMessage voiceCreated = await SendAsync(
            HttpMethod.Post,
            "/voice-vocabs/brands",
            new { value = "Acme" });
        Assert.Equal(HttpStatusCode.Created, voiceCreated.StatusCode);

        using HttpResponseMessage voiceUpdated = await SendAsync(
            HttpMethod.Put,
            "/api/voice-vocabs/brands",
            new { oldValue = "Acme", newValue = "Acme 2" });
        Assert.Equal(HttpStatusCode.OK, voiceUpdated.StatusCode);

        using HttpResponseMessage voiceDeleted = await SendAsync(
            HttpMethod.Delete,
            "/voice-vocabs/brands",
            new { value = "Acme 2" });
        Assert.Equal(HttpStatusCode.OK, voiceDeleted.StatusCode);

        string[] expectedActions =
        [
            "update_zalo_settings",
            "update_telegram_settings",
            "create_telegram_recipient",
            "update_telegram_recipient",
            "delete_telegram_recipient",
            "create_voice_vocab",
            "update_voice_vocab",
            "delete_voice_vocab",
        ];
        Assert.Equal(expectedActions, factory.ActivityLogs.Entries.Select(static entry => entry.Action));
        Assert.All(factory.ActivityLogs.Entries, static entry =>
            Assert.Equal(IntegrationActivityLogWebApplicationFactory.ActorName, entry.UserName));
        Assert.Equal(
        [
            "mutation:zalo", "audit:update_zalo_settings",
            "mutation:telegram-settings", "audit:update_telegram_settings",
            "mutation:telegram-create", "audit:create_telegram_recipient",
            "mutation:telegram-update", "audit:update_telegram_recipient",
            "mutation:telegram-delete", "audit:delete_telegram_recipient",
            "mutation:voice", "audit:create_voice_vocab",
            "mutation:voice", "audit:update_voice_vocab",
            "mutation:voice", "audit:delete_voice_vocab",
        ], factory.Sequence);

        string auditPayload = string.Join('|', factory.ActivityLogs.Entries.SelectMany(static entry =>
            entry.Details.Select(detail => $"{detail.Field}:{detail.OldValue}:{detail.NewValue}")));
        Assert.DoesNotContain(
            IntegrationActivityLogWebApplicationFactory.SensitiveSecret,
            auditPayload,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            IntegrationActivityLogWebApplicationFactory.SensitiveChatId,
            auditPayload,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mutation_WhenActivityLogWriterFails_ShouldKeepCommittedResponseSuccessful()
    {
        factory.Reset();
        factory.ActivityLogs.RejectWrites = true;
        try
        {
            using HttpResponseMessage response = await SendAsync(
                HttpMethod.Put,
                "/api/telegram/settings",
                new { enabled = false });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(factory.ProviderSettings.Enabled);
            Assert.Contains(factory.ActivityLogs.Attempts, static entry =>
                entry.Action == "update_telegram_settings");
        }
        finally
        {
            factory.ActivityLogs.RejectWrites = false;
        }
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        HttpRequestMessage request = new(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IntegrationActivityLogWebApplicationFactory.CreateToken());
        return factory.Client.SendAsync(request);
    }
}

public sealed class IntegrationActivityLogWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string JwtSecret = "activity-contract-secret-at-least-thirty-two-bytes";
    private const string UserId = "507f1f77bcf86cd799439011";
    public const string ActorName = "Synthetic Audit Admin";
    public const string RecipientId = "507f1f77bcf86cd799439012";
    public const string SensitiveSecret = "synthetic-sensitive-zalo-secret";
    public const string SensitiveChatId = "synthetic-sensitive-telegram-chat-id";

    public IntegrationActivityLogWebApplicationFactory()
    {
        Sequence = [];
        ActivityLogs = new FakeActivityLogWriter(Sequence);
        ProviderSettings = new FakeProviderSettingsRepository(Sequence);
        Voice = new FakeVoiceVocabularyRepository(Sequence);
        Client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public HttpClient Client { get; }

    public List<string> Sequence { get; }

    public FakeActivityLogWriter ActivityLogs { get; }

    public FakeProviderSettingsRepository ProviderSettings { get; }

    public FakeVoiceVocabularyRepository Voice { get; }

    public void Reset()
    {
        Sequence.Clear();
        ActivityLogs.Reset();
        ProviderSettings.Reset();
        Voice.Reset();
    }

    public static string CreateToken()
    {
        DateTime now = DateTime.UtcNow;
        Claim[] claims =
        [
            new("userId", UserId),
            new("role", "admin"),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = JwtSecret,
                ["JWT_SECRET"] = JwtSecret,
                ["LegacyCompatibility:AdminFullAccess"] = "true",
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserIdentityReader>();
            services.RemoveAll<IProviderSettingsRepository>();
            services.RemoveAll<IVoiceVocabularyRepository>();
            services.RemoveAll<IVoiceVocabularyRuntime>();
            services.RemoveAll<IActivityLogWriter>();

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
            });

            services.AddSingleton<IUserIdentityReader>(new FakeIdentityReader());
            services.AddSingleton<IProviderSettingsRepository>(ProviderSettings);
            services.AddSingleton<IVoiceVocabularyRepository>(Voice);
            services.AddSingleton<IVoiceVocabularyRuntime>(new FakeVoiceVocabularyRuntime());
            services.AddSingleton<IActivityLogWriter>(ActivityLogs);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Client.Dispose();
        base.Dispose(disposing);
    }

    private sealed class FakeIdentityReader : IUserIdentityReader
    {
        public Task<UserIdentitySnapshot?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<UserIdentitySnapshot?>(new UserIdentitySnapshot(
                UserId,
                "admin@example.test",
                "0900000000",
                ActorName,
                "admin",
                [],
                ["voice.manage"],
                null));
    }

    private sealed class FakeVoiceVocabularyRuntime : IVoiceVocabularyRuntime
    {
        public void Refresh(VoiceVocabulary vocabulary)
        {
        }
    }
}

public sealed class FakeActivityLogWriter(List<string> sequence) : IActivityLogWriter
{
    public bool RejectWrites { get; set; }

    public List<ActivityLogWriteEntry> Attempts { get; } = [];

    public List<ActivityLogWriteEntry> Entries { get; } = [];

    public void Reset()
    {
        RejectWrites = false;
        Attempts.Clear();
        Entries.Clear();
    }

    public Task AppendAsync(ActivityLogWriteEntry entry, CancellationToken cancellationToken)
    {
        Attempts.Add(entry);
        if (RejectWrites)
        {
            throw new InvalidOperationException("synthetic sensitive writer failure");
        }
        Entries.Add(entry);
        sequence.Add($"audit:{entry.Action}");
        return Task.CompletedTask;
    }

    public Task AppendManyAsync(
        IReadOnlyCollection<ActivityLogWriteEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (ActivityLogWriteEntry entry in entries)
        {
            AppendAsync(entry, cancellationToken).GetAwaiter().GetResult();
        }
        return Task.CompletedTask;
    }
}

public sealed class FakeProviderSettingsRepository(List<string> sequence) : IProviderSettingsRepository
{
    private TelegramRecipient? recipient;

    public bool Enabled { get; private set; }

    public void Reset()
    {
        Enabled = false;
        recipient = null;
    }

    public Task<TelegramSettings> GetTelegramAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new TelegramSettings(Enabled, recipient is null ? [] : [recipient]));

    public Task<TelegramSettings> SetTelegramEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        Enabled = enabled;
        sequence.Add("mutation:telegram-settings");
        return GetTelegramAsync(cancellationToken);
    }

    public Task<TelegramRecipient> AddTelegramRecipientAsync(
        TelegramRecipientInput input,
        CancellationToken cancellationToken)
    {
        recipient = new TelegramRecipient(
            IntegrationActivityLogWebApplicationFactory.RecipientId,
            input.Label ?? string.Empty,
            input.ChatId ?? string.Empty,
            input.Type ?? "personal",
            input.Enabled ?? true,
            input.NotifyTypes ?? []);
        sequence.Add("mutation:telegram-create");
        return Task.FromResult(recipient);
    }

    public Task<TelegramRecipient?> UpdateTelegramRecipientAsync(
        string id,
        TelegramRecipientInput input,
        CancellationToken cancellationToken)
    {
        if (recipient is null || recipient.Id != id)
        {
            return Task.FromResult<TelegramRecipient?>(null);
        }
        recipient = recipient with
        {
            Label = input.Label ?? recipient.Label,
            ChatId = input.ChatId ?? recipient.ChatId,
            Type = input.Type ?? recipient.Type,
            Enabled = input.Enabled ?? recipient.Enabled,
            NotifyTypes = input.NotifyTypes ?? recipient.NotifyTypes,
        };
        sequence.Add("mutation:telegram-update");
        return Task.FromResult<TelegramRecipient?>(recipient);
    }

    public Task<bool> DeleteTelegramRecipientAsync(string id, CancellationToken cancellationToken)
    {
        if (recipient is null || recipient.Id != id) return Task.FromResult(false);
        recipient = null;
        sequence.Add("mutation:telegram-delete");
        return Task.FromResult(true);
    }

    public Task<ZaloSettings> GetZaloAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ZaloSettings(string.Empty, string.Empty, string.Empty, false, null, false));

    public Task<ZaloSettings> UpdateZaloAsync(
        ZaloSettingsInput input,
        CancellationToken cancellationToken)
    {
        sequence.Add("mutation:zalo");
        return Task.FromResult(new ZaloSettings(
            input.AppId ?? string.Empty,
            input.OaId ?? string.Empty,
            input.RecipientUserId ?? string.Empty,
            false,
            null,
            !string.IsNullOrWhiteSpace(input.SecretKey)));
    }

    public Task<string?> GetZaloSecretKeyAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task SaveZaloOAuthTokensAsync(
        string accessToken,
        string? refreshToken,
        DateTimeOffset expiresAt,
        string? oaId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FakeVoiceVocabularyRepository(List<string> sequence) : IVoiceVocabularyRepository
{
    private VoiceVocabulary value = Initial();

    public void Reset() => value = Initial();

    public Task<VoiceVocabulary?> FindAsync(CancellationToken cancellationToken) =>
        Task.FromResult<VoiceVocabulary?>(value);

    public Task<VoiceVocabulary?> SaveAsync(
        VoiceVocabulary vocabulary,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        if (expectedVersion != value.Version)
        {
            return Task.FromResult<VoiceVocabulary?>(null);
        }
        value = vocabulary with { Version = checked(expectedVersion + 1) };
        sequence.Add("mutation:voice");
        return Task.FromResult<VoiceVocabulary?>(value);
    }

    private static VoiceVocabulary Initial() => VoiceVocabularyDefaults.Create() with { Version = 1 };
}
