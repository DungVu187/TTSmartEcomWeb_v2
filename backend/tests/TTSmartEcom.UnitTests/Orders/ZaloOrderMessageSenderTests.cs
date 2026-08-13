using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Integrations;
using TTSmartEcom.Application.Orders;

namespace TTSmartEcom.UnitTests.Orders;

public sealed class ZaloOrderMessageSenderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 1, 2, 0, TimeSpan.Zero);

    [Fact]
    public async Task SendAsync_WhenTokenIsFresh_ShouldSendLegacyHeaderAndPayloadWithoutRefresh()
    {
        FakeCredentialRepository repository = new(Credentials("current-access", Now.AddHours(1)));
        CapturingHandler handler = new(_ => JsonResponse("{\"error\":0}"));
        ZaloOrderMessageSender sender = Create(repository, handler);

        bool sent = await sender.SendAsync("Synthetic order message", CancellationToken.None);

        Assert.True(sent);
        Assert.Empty(repository.Updates);
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("https://openapi.zalo.me/v2.0/oa/message/cs", request.Uri.AbsoluteUri);
        Assert.Equal("current-access", request.AccessToken);
        Assert.Null(request.SecretKey);
        using JsonDocument body = JsonDocument.Parse(request.Body);
        Assert.Equal("synthetic-user-id", body.RootElement.GetProperty("recipient").GetProperty("user_id").GetString());
        Assert.Equal("Synthetic order message", body.RootElement.GetProperty("message").GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"error\":false")]
    [InlineData(",\"error\":0")]
    public async Task SendAsync_WhenTokenExpiresSoon_ShouldAcceptLegacyRefreshSuccessShapesAndPersistTokens(
        string errorFragment)
    {
        FakeCredentialRepository repository = new(Credentials("expiring-access", Now.AddMinutes(5)));
        CapturingHandler handler = new(request => request.Uri.Host == "oauth.example.test"
            ? JsonResponse($"{{\"access_token\":\"fresh-access\",\"refresh_token\":\"fresh-refresh\",\"expires_in\":3600{errorFragment}}}")
            : JsonResponse("{\"error\":0}"));
        ZaloOrderMessageSender sender = Create(repository, handler);

        bool sent = await sender.SendAsync("Synthetic order message", CancellationToken.None);

        Assert.True(sent);
        TokenUpdate update = Assert.Single(repository.Updates);
        Assert.Equal("synthetic-config-id", update.ConfigurationId);
        Assert.Equal(7, update.ExpectedVersion);
        Assert.Equal("fresh-access", update.AccessToken);
        Assert.Equal("fresh-refresh", update.RefreshToken);
        Assert.Equal(Now.AddHours(1), update.ExpiresAt);
        Assert.Equal(2, handler.Requests.Count);
        CapturedRequest refresh = handler.Requests[0];
        Assert.Equal("https://oauth.example.test/token", refresh.Uri.AbsoluteUri);
        Assert.Equal("synthetic-secret", refresh.SecretKey);
        Assert.Null(refresh.AccessToken);
        Assert.Contains("refresh_token=synthetic-refresh", refresh.Body, StringComparison.Ordinal);
        Assert.Contains("app_id=synthetic-app", refresh.Body, StringComparison.Ordinal);
        Assert.Equal("fresh-access", handler.Requests[1].AccessToken);
    }

    [Fact]
    public async Task SendAsync_WhenRefreshCompareAndSwapLoses_ShouldRereadAndUseWinnerToken()
    {
        FakeCredentialRepository repository = new(
            [
                Credentials("expiring-access", Now.AddMinutes(5)),
                Credentials("winner-access", Now.AddHours(2)) with { Version = 8 },
            ])
        {
            SaveResult = false,
        };
        CapturingHandler handler = new(request => request.Uri.Host == "oauth.example.test"
            ? JsonResponse("{\"error\":0,\"access_token\":\"loser-access\",\"refresh_token\":\"loser-refresh\",\"expires_in\":3600}")
            : JsonResponse("{\"error\":0}"));
        ZaloOrderMessageSender sender = Create(repository, handler);

        bool sent = await sender.SendAsync("Synthetic order message", CancellationToken.None);

        Assert.True(sent);
        Assert.Equal(2, repository.FindCalls);
        Assert.Single(repository.Updates);
        Assert.Equal("winner-access", handler.Requests[1].AccessToken);
    }

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("oversized")]
    [InlineData("provider-error")]
    [InlineData("timeout")]
    public async Task SendAsync_WhenProviderResponseFailsValidation_ShouldReturnFalse(string failure)
    {
        FakeCredentialRepository repository = new(Credentials("current-access", Now.AddHours(1)));
        CapturingHandler handler = new(_ => failure switch
        {
            "invalid-json" => JsonResponse("not-json"),
            "oversized" => JsonResponse(new string('x', 512)),
            "provider-error" => JsonResponse("{\"error\":-216}"),
            "timeout" => throw new OperationCanceledException("synthetic timeout"),
            _ => throw new InvalidOperationException("Unknown synthetic failure"),
        });
        ZaloOrderMessageSender sender = Create(repository, handler, maximumResponseBytes: 128);

        bool sent = await sender.SendAsync("Synthetic order message", CancellationToken.None);

        Assert.False(sent);
    }

    [Fact]
    public async Task SendAsync_WhenTransportFails_ShouldNotLogTokenRecipientOrMessage()
    {
        const string accessToken = "synthetic-sensitive-access-token";
        const string message = "Synthetic Customer 0900000000";
        FakeCredentialRepository repository = new(Credentials(accessToken, Now.AddHours(1)));
        ListLogger logger = new();
        CapturingHandler handler = new(_ => throw new HttpRequestException(
            $"synthetic failure {accessToken} synthetic-user-id {message}"));
        ZaloOrderMessageSender sender = Create(repository, handler, logger: logger);

        bool sent = await sender.SendAsync(message, CancellationToken.None);

        Assert.False(sent);
        string logs = string.Join('\n', logger.Entries.Select(static entry => entry.Message));
        Assert.DoesNotContain(accessToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-user-id", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("0900000000", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic Customer", logs, StringComparison.Ordinal);
        Assert.Contains(nameof(HttpRequestException), logs, StringComparison.Ordinal);
    }

    private static ZaloOrderMessageSender Create(
        FakeCredentialRepository repository,
        HttpMessageHandler handler,
        int maximumResponseBytes = 65_536,
        ILogger<ZaloOrderMessageSender>? logger = null) => new(
        repository,
        new FakeHttpClientFactory(new HttpClient(handler)),
        Options.Create(new ZaloOAuthOptions
        {
            StateSecret = new string('s', 32),
            TokenEndpoint = "https://oauth.example.test/token",
            AuthorizationEndpoint = "https://oauth.example.test/permission",
            MaxProviderResponseBytes = maximumResponseBytes,
        }),
        new FixedTimeProvider(Now),
        logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ZaloOrderMessageSender>.Instance);

    private static ZaloOrderDeliveryCredentials Credentials(
        string accessToken,
        DateTimeOffset expiresAt) => new(
        "synthetic-config-id",
        7,
        "synthetic-app",
        "synthetic-secret",
        "synthetic-user-id",
        accessToken,
        "synthetic-refresh",
        expiresAt);

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler(
        Func<CapturedRequest, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedRequest captured = new(
                request.RequestUri ?? throw new InvalidOperationException("Request URI is required"),
                Header(request, "access_token"),
                Header(request, "secret_key"),
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Add(captured);
            return responder(captured);
        }

        private static string? Header(HttpRequestMessage request, string name) =>
            request.Headers.TryGetValues(name, out IEnumerable<string>? values)
                ? values.Single()
                : null;
    }

    private sealed class FakeCredentialRepository : IZaloOrderCredentialRepository
    {
        private readonly IReadOnlyList<ZaloOrderDeliveryCredentials> responses;

        public FakeCredentialRepository(ZaloOrderDeliveryCredentials response)
            : this([response])
        {
        }

        public FakeCredentialRepository(IReadOnlyList<ZaloOrderDeliveryCredentials> responses)
        {
            this.responses = responses;
        }

        public int FindCalls { get; private set; }

        public bool SaveResult { get; init; } = true;

        public List<TokenUpdate> Updates { get; } = [];

        public Task<ZaloOrderDeliveryCredentials?> FindAsync(CancellationToken cancellationToken)
        {
            ZaloOrderDeliveryCredentials result = responses[Math.Min(FindCalls, responses.Count - 1)];
            FindCalls++;
            return Task.FromResult<ZaloOrderDeliveryCredentials?>(result);
        }

        public Task<bool> TryUpdateTokensAsync(
            string configurationId,
            int expectedVersion,
            string accessToken,
            string refreshToken,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Updates.Add(new TokenUpdate(
                configurationId,
                expectedVersion,
                accessToken,
                refreshToken,
                expiresAt));
            return Task.FromResult(SaveResult);
        }
    }

    private sealed class ListLogger : ILogger<ZaloOrderMessageSender>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private sealed record CapturedRequest(
        Uri Uri,
        string? AccessToken,
        string? SecretKey,
        string Body);

    private sealed record TokenUpdate(
        string ConfigurationId,
        int ExpectedVersion,
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt);

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
