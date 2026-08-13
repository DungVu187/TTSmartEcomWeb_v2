using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Domain.Integrations;

namespace TTSmartEcom.UnitTests.Integrations;

public sealed class ZaloOAuthServiceTests
{
    [Fact]
    public async Task Callback_ConsumesStateOnce_AndPersistsTokensWithoutReturningThem()
    {
        FakeRepository repository = new();
        FakeState state = new();
        FakeClient client = new();
        ZaloOAuthService service = new(repository, state, client, TimeProvider.System);

        ZaloOAuthAuthorizationResult authorization = await service.CreateAuthorizationUrlAsync(
            "admin-1",
            "https://api.example.test/zalo/callback",
            CancellationToken.None);
        ZaloOAuthCallbackResult callback = await service.CompleteAsync(
            "synthetic-code",
            state.LastState,
            "https://api.example.test/zalo/callback",
            "oa-1",
            CancellationToken.None);
        ZaloOAuthCallbackResult replay = await service.CompleteAsync(
            "synthetic-code",
            state.LastState,
            "https://api.example.test/zalo/callback",
            "oa-1",
            CancellationToken.None);

        Assert.Equal(ZaloOAuthAuthorizationStatus.Success, authorization.Status);
        Assert.Equal(ZaloOAuthCallbackStatus.Success, callback.Status);
        Assert.Equal(ZaloOAuthCallbackStatus.InvalidState, replay.Status);
        Assert.Equal("synthetic-access", repository.AccessToken);
        Assert.Equal("synthetic-refresh", repository.RefreshToken);
        Assert.Equal("oa-1", repository.OaId);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Callback_WithProviderFailure_ConsumesState_AndDoesNotPersistTokens()
    {
        FakeRepository repository = new();
        FakeState state = new();
        FakeClient client = new() { Result = new(ZaloOAuthTokenExchangeStatus.ProviderRejected) };
        ZaloOAuthService service = new(repository, state, client, TimeProvider.System);
        await service.CreateAuthorizationUrlAsync("admin-1", "https://api.example.test/zalo/callback", CancellationToken.None);

        ZaloOAuthCallbackResult result = await service.CompleteAsync(
            "synthetic-code",
            state.LastState,
            "https://api.example.test/zalo/callback",
            null,
            CancellationToken.None);

        Assert.Equal(ZaloOAuthCallbackStatus.ProviderRejected, result.Status);
        Assert.Null(repository.AccessToken);
        Assert.False(state.TryConsume(state.LastState!, "https://api.example.test/zalo/callback"));
    }

    [Fact]
    public async Task AuthorizationUrl_WithoutSecureStateConfiguration_FailsClosed()
    {
        FakeState state = new() { IsAvailable = false };
        ZaloOAuthService service = new(new FakeRepository(), state, new FakeClient(), TimeProvider.System);

        ZaloOAuthAuthorizationResult result = await service.CreateAuthorizationUrlAsync(
            "admin-1",
            "https://api.example.test/zalo/callback",
            CancellationToken.None);

        Assert.Equal(ZaloOAuthAuthorizationStatus.StateUnavailable, result.Status);
    }

    [Fact]
    public async Task Callback_WithOversizedStateOrOaId_ShouldRejectBeforeStateAndProvider()
    {
        FakeState state = new();
        FakeClient client = new();
        ZaloOAuthService service = new(new FakeRepository(), state, client, TimeProvider.System);

        ZaloOAuthCallbackResult stateResult = await service.CompleteAsync(
            "synthetic-code", new string('s', 4_097), "https://api.example.test/zalo/callback", null, CancellationToken.None);
        ZaloOAuthCallbackResult oaResult = await service.CompleteAsync(
            "synthetic-code", "synthetic-state", "https://api.example.test/zalo/callback", new string('o', 257), CancellationToken.None);

        Assert.Equal(ZaloOAuthCallbackStatus.InvalidRequest, stateResult.Status);
        Assert.Equal(ZaloOAuthCallbackStatus.InvalidRequest, oaResult.Status);
        Assert.Equal(0, client.Calls);
    }

    private sealed class FakeRepository : IProviderSettingsRepository
    {
        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public string? OaId { get; private set; }

        public Task<ZaloSettings> GetZaloAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ZaloSettings("app-1", "", "", false, null, true));

        public Task<string?> GetZaloSecretKeyAsync(CancellationToken cancellationToken) => Task.FromResult<string?>("secret-1");

        public Task SaveZaloOAuthTokensAsync(string accessToken, string? refreshToken, DateTimeOffset expiresAt, string? oaId, CancellationToken cancellationToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            OaId = oaId;
            return Task.CompletedTask;
        }

        public Task<ZaloSettings> UpdateZaloAsync(ZaloSettingsInput input, CancellationToken cancellationToken) => GetZaloAsync(cancellationToken);
        public Task<TelegramSettings> GetTelegramAsync(CancellationToken cancellationToken) => Task.FromResult(new TelegramSettings(false, []));
        public Task<TelegramSettings> SetTelegramEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.FromResult(new TelegramSettings(enabled, []));
        public Task<TelegramRecipient> AddTelegramRecipientAsync(TelegramRecipientInput input, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TelegramRecipient?> UpdateTelegramRecipientAsync(string id, TelegramRecipientInput input, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteTelegramRecipientAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeState : IZaloOAuthStateService
    {
        private bool consumed;
        public bool IsAvailable { get; set; } = true;
        public string? LastState { get; private set; }

        public bool TryCreate(string subject, string redirectUri, out string state)
        {
            state = LastState = "synthetic-state";
            consumed = false;
            return IsAvailable;
        }

        public bool TryConsume(string state, string redirectUri)
        {
            if (!IsAvailable || consumed || state != LastState) return false;
            consumed = true;
            return true;
        }
    }

    private sealed class FakeClient : IZaloOAuthClient
    {
        public int Calls { get; private set; }
        public ZaloOAuthTokenExchangeResult Result { get; init; } = new(
            ZaloOAuthTokenExchangeStatus.Success,
            "synthetic-access",
            "synthetic-refresh",
            3600);

        public string? BuildAuthorizationUrl(string appId, string redirectUri, string state) =>
            $"https://oauth.example.test/permission?state={state}";

        public Task<ZaloOAuthTokenExchangeResult> ExchangeCodeAsync(string appId, string secretKey, string code, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }
}
