using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Domain.Integrations;

namespace TTSmartEcom.UnitTests.Integrations;

public sealed class ProviderSettingsServiceTests
{
    [Fact]
    public async Task TelegramSender_WithMissingToken_ShouldFailWithoutCallingHttpClient()
    {
        CountingHandler handler = new();
        TTSmartEcom.Api.Integrations.TelegramMessageSender sender = new(
            new FakeHttpClientFactory(new HttpClient(handler)),
            Microsoft.Extensions.Options.Options.Create(new TTSmartEcom.Api.Configuration.ExternalServicesOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TTSmartEcom.Api.Integrations.TelegramMessageSender>.Instance);

        Assert.False(await sender.SendAsync("123", "test", CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task AddRecipient_WithInvalidType_ShouldRejectBeforeRepository()
    {
        FakeRepository repository = new();
        ProviderSettingsService service = new(repository);

        TTSmartEcom.Application.Common.Errors.ApplicationException error = await Assert.ThrowsAsync<TTSmartEcom.Application.Common.Errors.ApplicationException>(
            () => service.AddRecipientAsync(new TelegramRecipientInput("Ops", "123", "channel", true, ["new_order"]), CancellationToken.None));

        Assert.Equal(400, error.Error.HttpStatus);
        Assert.False(repository.AddCalled);
    }

    [Fact]
    public async Task AddRecipient_ShouldNormalizeAndPersist()
    {
        FakeRepository repository = new();
        ProviderSettingsService service = new(repository);

        TelegramRecipient recipient = await service.AddRecipientAsync(new TelegramRecipientInput(" Ops ", " 123 ", "GROUP", null, ["new_order", "new_order"]), CancellationToken.None);

        Assert.Equal("Ops", recipient.Label);
        Assert.Equal("123", recipient.ChatId);
        Assert.Equal("group", recipient.Type);
        Assert.Single(recipient.NotifyTypes);
    }

    private sealed class FakeRepository : IProviderSettingsRepository
    {
        public bool AddCalled { get; private set; }
        public Task<TelegramSettings> GetTelegramAsync(CancellationToken cancellationToken) => Task.FromResult(new TelegramSettings(false, []));
        public Task<TelegramSettings> SetTelegramEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.FromResult(new TelegramSettings(enabled, []));
        public Task<TelegramRecipient> AddTelegramRecipientAsync(TelegramRecipientInput input, CancellationToken cancellationToken) { AddCalled = true; return Task.FromResult(new TelegramRecipient("0123456789abcdef01234567", input.Label ?? "", input.ChatId ?? "", input.Type ?? "personal", input.Enabled ?? true, input.NotifyTypes ?? [])); }
        public Task<TelegramRecipient?> UpdateTelegramRecipientAsync(string id, TelegramRecipientInput input, CancellationToken cancellationToken) => Task.FromResult<TelegramRecipient?>(null);
        public Task<bool> DeleteTelegramRecipientAsync(string id, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<ZaloSettings> GetZaloAsync(CancellationToken cancellationToken) => Task.FromResult(new ZaloSettings("", "", "", false, null, false));
        public Task<ZaloSettings> UpdateZaloAsync(ZaloSettingsInput input, CancellationToken cancellationToken) => Task.FromResult(new ZaloSettings(input.AppId ?? "", input.OaId ?? "", input.RecipientUserId ?? "", false, null, !string.IsNullOrWhiteSpace(input.SecretKey)));
        public Task<string?> GetZaloSecretKeyAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task SaveZaloOAuthTokensAsync(string accessToken, string? refreshToken, DateTimeOffset expiresAt, string? oaId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
