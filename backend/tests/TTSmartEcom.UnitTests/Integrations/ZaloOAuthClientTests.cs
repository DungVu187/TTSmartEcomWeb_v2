using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Integrations;
using TTSmartEcom.Application.Integrations;

namespace TTSmartEcom.UnitTests.Integrations;

public sealed class ZaloOAuthClientTests
{
    [Fact]
    public async Task Exchange_MapsTokens_AndSendsSecretOnlyInHeader()
    {
        CapturingHandler handler = new("{\"access_token\":\"access\",\"refresh_token\":\"refresh\",\"expires_in\":\"3600\"}");
        ZaloOAuthClient client = Create(handler);

        ZaloOAuthTokenExchangeResult result = await client.ExchangeCodeAsync("app", "secret", "code", CancellationToken.None);

        Assert.Equal(ZaloOAuthTokenExchangeStatus.Success, result.Status);
        Assert.Equal("access", result.AccessToken);
        Assert.Equal("refresh", result.RefreshToken);
        Assert.Equal(3600, result.ExpiresInSeconds);
        Assert.Equal("secret", handler.SecretHeader);
        Assert.DoesNotContain("secret", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exchange_WithOversizedResponse_FailsWithoutParsingPayload()
    {
        CapturingHandler handler = new(new string('x', 2048));
        ZaloOAuthClient client = Create(handler, maxResponseBytes: 128);

        ZaloOAuthTokenExchangeResult result = await client.ExchangeCodeAsync("app", "secret", "code", CancellationToken.None);

        Assert.Equal(ZaloOAuthTokenExchangeStatus.InvalidResponse, result.Status);
    }

    private static ZaloOAuthClient Create(HttpMessageHandler handler, int maxResponseBytes = 65_536) => new(
        new FakeHttpClientFactory(new HttpClient(handler)),
        Options.Create(new ZaloOAuthOptions
        {
            StateSecret = new string('s', 32),
            TokenEndpoint = "https://oauth.example.test/token",
            AuthorizationEndpoint = "https://oauth.example.test/permission",
            MaxProviderResponseBytes = maxResponseBytes,
        }),
        NullLogger<ZaloOAuthClient>.Instance);

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler(string responseBody) : HttpMessageHandler
    {
        public string? SecretHeader { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SecretHeader = request.Headers.TryGetValues("secret_key", out IEnumerable<string>? values) ? values.Single() : null;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody),
            };
        }
    }
}
