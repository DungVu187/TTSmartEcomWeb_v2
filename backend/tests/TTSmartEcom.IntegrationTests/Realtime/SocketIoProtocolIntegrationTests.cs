using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Realtime;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Application.Realtime;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.IntegrationTests.Realtime;

public sealed class SocketIoProtocolIntegrationTests
{
    private const string AllowedOrigin = "http://localhost:3000";
    private const string AdminUserId = "507f1f77bcf86cd799439011";
    private const string OtherAdminUserId = "507f1f77bcf86cd799439012";
    private const string CustomerUserId = "507f1f77bcf86cd799439013";
    private const string ControlPlaneSuperAdminUserId = "b3a89f4d-0b53-4c94-9556-9a95d2648c72";

    [Fact]
    public async Task WebSocketFirst_ConnectsAndReceivesAllFourOrderEvents()
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();
        using WebSocket socket = await host.ConnectWebSocketAsync(
            "/socket.io?EIO=4&transport=websocket",
            host.AdminToken,
            AllowedOrigin,
            timeout.Token);

        string openPacket = await ReceiveTextAsync(socket, timeout.Token);
        AssertOpenPacket(openPacket, expectsUpgrade: false);

        await SendTextAsync(socket, "40", timeout.Token);
        AssertConnectPacket(await ReceiveApplicationPacketAsync(socket, timeout.Token));

        IOrderRealtimePublisher publisher = host.Services.GetRequiredService<IOrderRealtimePublisher>();
        await publisher.PublishCreatedAsync(
            new OrderCreatedRealtimeEvent(
                "order-1",
                "TTS-01",
                "0900000000",
                125_000m,
                DateTimeOffset.Parse("2026-08-13T03:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
            timeout.Token);
        await publisher.PublishUpdatedAsync(
            new OrderUpdatedRealtimeEvent("order-1", "status", "Completed"),
            timeout.Token);
        await publisher.PublishCancelledAsync(
            new OrderCancelledRealtimeEvent("order-1", "0900000000"),
            timeout.Token);
        await publisher.PublishDeletedAsync(
            new OrderDeletedRealtimeEvent("order-1"),
            timeout.Token);

        AssertEvent(
            await ReceiveApplicationPacketAsync(socket, timeout.Token),
            "order_created",
            payload =>
            {
                Assert.Equal("order-1", payload.GetProperty("orderId").GetString());
                Assert.Equal("TTS-01", payload.GetProperty("orderCode").GetString());
                Assert.Equal(125_000m, payload.GetProperty("total").GetDecimal());
            });
        AssertEvent(
            await ReceiveApplicationPacketAsync(socket, timeout.Token),
            "order_updated",
            payload =>
            {
                Assert.Equal("status", payload.GetProperty("updatedField").GetString());
                Assert.Equal("Completed", payload.GetProperty("newValue").GetString());
            });
        AssertEvent(
            await ReceiveApplicationPacketAsync(socket, timeout.Token),
            "order_cancelled",
            payload => Assert.Equal("0900000000", payload.GetProperty("userPhone").GetString()));
        AssertEvent(
            await ReceiveApplicationPacketAsync(socket, timeout.Token),
            "order_deleted",
            payload => Assert.Equal("order-1", payload.GetProperty("orderId").GetString()));
    }

    [Fact]
    public async Task WebSocketFirst_ControlPlaneSuperAdmin_ConnectsWithoutOperationalUser()
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();
        using WebSocket socket = await host.ConnectWebSocketAsync(
            "/socket.io?EIO=4&transport=websocket",
            host.ControlPlaneSuperAdminToken,
            AllowedOrigin,
            timeout.Token);

        AssertOpenPacket(await ReceiveTextAsync(socket, timeout.Token), expectsUpgrade: false);
        await SendTextAsync(socket, "40", timeout.Token);
        AssertConnectPacket(await ReceiveApplicationPacketAsync(socket, timeout.Token));
    }

    [Fact]
    public async Task PollingApiAlias_ConnectsAndCompletesHeartbeat()
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();

        string sid = await OpenPollingAsync(host, "/api/socket.io", host.AdminToken, timeout.Token);
        await PostPollingAsync(host, "/api/socket.io", sid, host.AdminToken, "40", HttpStatusCode.OK, timeout.Token);

        string connect = await GetPollingAsync(host, "/api/socket.io", sid, host.AdminToken, timeout.Token);
        AssertConnectPacket(connect);
        string ping = await GetPollingAsync(host, "/api/socket.io", sid, host.AdminToken, timeout.Token);
        Assert.Equal("2", ping);

        string response = await PostPollingAsync(
            host,
            "/api/socket.io",
            sid,
            host.AdminToken,
            "3",
            HttpStatusCode.OK,
            timeout.Token);
        Assert.Equal("ok", response);
    }

    [Fact]
    public async Task PollingSession_UpgradesToWebSocketAndReceivesEventThere()
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();

        string sid = await OpenPollingAsync(host, "/api/socket.io", host.AdminToken, timeout.Token);
        await PostPollingAsync(host, "/api/socket.io", sid, host.AdminToken, "40", HttpStatusCode.OK, timeout.Token);
        AssertConnectPacket(await GetPollingAsync(
            host,
            "/api/socket.io",
            sid,
            host.AdminToken,
            timeout.Token));

        using WebSocket socket = await host.ConnectWebSocketAsync(
            $"/api/socket.io?EIO=4&transport=websocket&sid={sid}",
            host.AdminToken,
            AllowedOrigin,
            timeout.Token);
        await SendTextAsync(socket, "2probe", timeout.Token);
        Assert.Equal("3probe", await ReceiveTextAsync(socket, timeout.Token));
        Assert.Equal("6", await ReadUntilNoopAsync(host, sid, timeout.Token));

        await SendTextAsync(socket, "5", timeout.Token);
        Assert.Equal("2", await ReceiveTextAsync(socket, timeout.Token));
        await SendTextAsync(socket, "3", timeout.Token);
        IOrderRealtimePublisher publisher = host.Services.GetRequiredService<IOrderRealtimePublisher>();
        await publisher.PublishDeletedAsync(new OrderDeletedRealtimeEvent("upgraded-order"), timeout.Token);

        AssertEvent(
            await ReceiveApplicationPacketAsync(socket, timeout.Token),
            "order_deleted",
            payload => Assert.Equal("upgraded-order", payload.GetProperty("orderId").GetString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamespaceConnect_RejectsMissingCookieOrCustomerRole(bool useCustomerCookie)
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();
        string? token = useCustomerCookie ? host.CustomerToken : null;
        using WebSocket socket = await host.ConnectWebSocketAsync(
            "/socket.io?EIO=4&transport=websocket",
            token,
            AllowedOrigin,
            timeout.Token);

        AssertOpenPacket(await ReceiveTextAsync(socket, timeout.Token), expectsUpgrade: false);
        await SendTextAsync(socket, "40", timeout.Token);

        Assert.Equal("44{\"message\":\"unauthorized\"}", await ReceiveTextAsync(socket, timeout.Token));
    }

    [Fact]
    public async Task Handshake_RejectsOriginOutsideAllowlist()
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();
        using HttpRequestMessage request = SocketIoTestHost.Request(
            HttpMethod.Get,
            "/socket.io?EIO=4&transport=polling",
            host.AdminToken,
            "https://attacker.example");
        using HttpResponseMessage response = await host.Client.SendAsync(request, timeout.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PollingSession_RejectsCookieChangeAndClosesSession()
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();
        string sid = await OpenPollingAsync(host, "/socket.io", host.AdminToken, timeout.Token);

        await PostPollingAsync(
            host,
            "/socket.io",
            sid,
            host.OtherAdminToken,
            "40",
            HttpStatusCode.BadRequest,
            timeout.Token);
        using HttpRequestMessage request = SocketIoTestHost.Request(
            HttpMethod.Get,
            $"/socket.io?EIO=4&transport=polling&sid={sid}",
            host.AdminToken,
            AllowedOrigin);
        using HttpResponseMessage response = await host.Client.SendAsync(request, timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PollingPost_RejectsPayloadOverConfiguredLimit()
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();
        string sid = await OpenPollingAsync(host, "/socket.io", host.AdminToken, timeout.Token);

        string oversized = new('x', SocketIoTestHost.MaxPayloadBytes + 1);
        await PostPollingAsync(
            host,
            "/socket.io",
            sid,
            host.AdminToken,
            oversized,
            HttpStatusCode.RequestEntityTooLarge,
            timeout.Token);
    }

    [Fact]
    public async Task Options_ReturnsCredentialedCorsHeadersForAllowedOrigin()
    {
        await using SocketIoTestHost host = await SocketIoTestHost.StartAsync();
        using CancellationTokenSource timeout = Timeout();
        using HttpRequestMessage request = SocketIoTestHost.Request(
            HttpMethod.Options,
            "/socket.io",
            null,
            AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        using HttpResponseMessage response = await host.Client.SendAsync(request, timeout.Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Contains("GET", response.Headers.GetValues("Access-Control-Allow-Methods").Single(), StringComparison.Ordinal);
        Assert.Equal(
            "content-type",
            response.Headers.GetValues("Access-Control-Allow-Headers").Single());
    }

    private static async Task<string> OpenPollingAsync(
        SocketIoTestHost host,
        string path,
        string token,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = SocketIoTestHost.Request(
            HttpMethod.Get,
            $"{path}?EIO=4&transport=polling",
            token,
            AllowedOrigin);
        using HttpResponseMessage response = await host.Client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string packet = await response.Content.ReadAsStringAsync(cancellationToken);
        AssertOpenPacket(packet, expectsUpgrade: true);
        using JsonDocument document = JsonDocument.Parse(packet[1..]);
        return Assert.IsType<string>(document.RootElement.GetProperty("sid").GetString());
    }

    private static async Task<string> GetPollingAsync(
        SocketIoTestHost host,
        string path,
        string sid,
        string token,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = SocketIoTestHost.Request(
            HttpMethod.Get,
            $"{path}?EIO=4&transport=polling&sid={sid}",
            token,
            AllowedOrigin);
        using HttpResponseMessage response = await host.Client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static async Task<string> PostPollingAsync(
        SocketIoTestHost host,
        string path,
        string sid,
        string token,
        string packet,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = SocketIoTestHost.Request(
            HttpMethod.Post,
            $"{path}?EIO=4&transport=polling&sid={sid}",
            token,
            AllowedOrigin);
        request.Content = new StringContent(packet, Encoding.UTF8, "text/plain");
        using HttpResponseMessage response = await host.Client.SendAsync(request, cancellationToken);
        Assert.Equal(expectedStatus, response.StatusCode);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static async Task<string> ReadUntilNoopAsync(
        SocketIoTestHost host,
        string sid,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string packet = await GetPollingAsync(
                host,
                "/api/socket.io",
                sid,
                host.AdminToken,
                cancellationToken);
            if (packet == "6")
            {
                return packet;
            }

            Assert.Equal("2", packet);
            await PostPollingAsync(
                host,
                "/api/socket.io",
                sid,
                host.AdminToken,
                "3",
                HttpStatusCode.OK,
                cancellationToken);
        }
    }

    private static void AssertOpenPacket(string packet, bool expectsUpgrade)
    {
        Assert.StartsWith("0", packet, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(packet[1..]);
        JsonElement root = document.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("sid").GetString()));
        Assert.Equal(SocketIoTestHost.MaxPayloadBytes, root.GetProperty("maxPayload").GetInt32());
        string[] upgrades = root.GetProperty("upgrades")
            .EnumerateArray()
            .Select(static value => value.GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(expectsUpgrade ? ["websocket"] : [], upgrades);
    }

    private static void AssertConnectPacket(string packet)
    {
        Assert.StartsWith("40", packet, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(packet[2..]);
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("sid").GetString()));
    }

    private static void AssertEvent(string packet, string expectedName, Action<JsonElement> assertPayload)
    {
        Assert.StartsWith("42", packet, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(packet[2..]);
        JsonElement root = document.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(expectedName, root[0].GetString());
        assertPayload(root[1]);
    }

    private static async Task<string> ReceiveApplicationPacketAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string packet = await ReceiveTextAsync(socket, cancellationToken);
            if (packet != "2")
            {
                return packet;
            }

            await SendTextAsync(socket, "3", cancellationToken);
        }
    }

    private static async Task<string> ReceiveTextAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8_192];
        ValueWebSocketReceiveResult result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        Assert.True(result.EndOfMessage);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static Task SendTextAsync(
        WebSocket socket,
        string packet,
        CancellationToken cancellationToken) => socket.SendAsync(
            Encoding.UTF8.GetBytes(packet),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);

    private static CancellationTokenSource Timeout() => new(TimeSpan.FromSeconds(8));

    private sealed class SocketIoTestHost : IAsyncDisposable
    {
        private const string JwtSecret = "socket-io-integration-jwt-secret-at-least-32-bytes";
        public const int MaxPayloadBytes = 128;
        private readonly WebApplication application;

        private SocketIoTestHost(WebApplication application)
        {
            this.application = application;
            Client = application.GetTestClient();
            Client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            AdminToken = CreateToken(AdminUserId);
            OtherAdminToken = CreateToken(OtherAdminUserId);
            CustomerToken = CreateToken(CustomerUserId);
            ControlPlaneSuperAdminToken = CreateToken(ControlPlaneSuperAdminUserId);
        }

        public HttpClient Client { get; }
        public IServiceProvider Services => application.Services;
        public string AdminToken { get; }
        public string OtherAdminToken { get; }
        public string CustomerToken { get; }
        public string ControlPlaneSuperAdminToken { get; }

        public static async Task<SocketIoTestHost> StartAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
            });
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{CorsOptions.SectionName}:AllowedOrigins:0"] = AllowedOrigin,
                [$"{SocketIoRealtimeOptions.SectionName}:PingIntervalMilliseconds"] = "500",
                [$"{SocketIoRealtimeOptions.SectionName}:PingTimeoutMilliseconds"] = "2000",
                [$"{SocketIoRealtimeOptions.SectionName}:ConnectTimeoutMilliseconds"] = "5000",
                [$"{SocketIoRealtimeOptions.SectionName}:UpgradeTimeoutMilliseconds"] = "2000",
                [$"{SocketIoRealtimeOptions.SectionName}:SendTimeoutMilliseconds"] = "2000",
                [$"{SocketIoRealtimeOptions.SectionName}:MaxPayloadBytes"] = MaxPayloadBytes.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            });
            builder.Logging.ClearProviders();
            builder.Services.AddRouting();
            builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        NameClaimType = "userId",
                        RoleClaimType = "role",
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            if (context.Request.Cookies.TryGetValue("authToken", out string? token))
                            {
                                context.Token = token;
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
            builder.Services.AddSingleton<IUserIdentityReader>(new FakeIdentityReader());
            builder.Services.AddSingleton<IControlPlaneIdentityReader>(new FakeControlPlaneIdentityReader());
            builder.Services.AddSingleton<IOrderService>(static _ =>
                throw new NotSupportedException("Order mutations are outside this protocol fixture."));
            builder.Services.AddTtsmartSocketIoRealtime(builder.Configuration);

            WebApplication application = builder.Build();
            application.UseTtsmartSocketIoRealtime();
            application.UseRouting();
            application.UseAuthentication();
            application.MapTtsmartSocketIoRealtime();
            await application.StartAsync();
            return new SocketIoTestHost(application);
        }

        public static HttpRequestMessage Request(
            HttpMethod method,
            string path,
            string? token,
            string? origin)
        {
            var request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Cookie", $"authToken={token}");
            }

            if (!string.IsNullOrEmpty(origin))
            {
                request.Headers.Add("Origin", origin);
            }

            return request;
        }

        public async Task<WebSocket> ConnectWebSocketAsync(
            string path,
            string? token,
            string? origin,
            CancellationToken cancellationToken)
        {
            WebSocketClient client = application.GetTestServer().CreateWebSocketClient();
            client.ConfigureRequest = context =>
            {
                if (!string.IsNullOrEmpty(token))
                {
                    context.Headers.Cookie = $"authToken={token}";
                }

                if (!string.IsNullOrEmpty(origin))
                {
                    context.Headers.Origin = origin;
                }
            };
            return await client.ConnectAsync(new Uri($"ws://localhost{path}"), cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.StopAsync();
            await application.DisposeAsync();
        }

        private static string CreateToken(string userId)
        {
            DateTime now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                claims:
                [
                    new Claim("userId", userId),
                    new Claim("role", userId == CustomerUserId ? "customer" : "admin"),
                    new Claim(
                        JwtRegisteredClaimNames.Iat,
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
    }

    private sealed class FakeIdentityReader : IUserIdentityReader
    {
        public Task<UserIdentitySnapshot?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            string? role = userId switch
            {
                AdminUserId or OtherAdminUserId => "admin",
                CustomerUserId => "customer",
                _ => null,
            };
            UserIdentitySnapshot? identity = role is null
                ? null
                : new UserIdentitySnapshot(
                    userId,
                    $"{role}@example.test",
                    "0900000000",
                    "Synthetic user",
                    role,
                    [],
                    [],
                    null);
            return Task.FromResult(identity);
        }
    }

    private sealed class FakeControlPlaneIdentityReader : IControlPlaneIdentityReader
    {
        public Task<ICurrentUserContext?> FindContextByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<ICurrentUserContext?>(userId.ToString() == ControlPlaneSuperAdminUserId
                ? new CurrentUserContext(
                    userId,
                    isAuthenticated: true,
                    isPlatformSuperAdmin: true,
                    displayName: "Control Plane Super Admin",
                    email: "superadmin@example.test",
                    phone: null,
                    companyMemberships: [],
                    activeCompanyId: null,
                    branchMemberships: [],
                    activeBranchId: null,
                    roles: ["superadmin"],
                    permissions: new HashSet<string>(StringComparer.Ordinal),
                    isControlPlaneIdentity: true)
                : null);

        public Task<ICurrentUserContext?> FindContextByLoginAsync(string identifier, CancellationToken cancellationToken) =>
            Task.FromResult<ICurrentUserContext?>(null);
    }
}
