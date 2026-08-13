using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Options;
using TTSmartEcom.Application.Realtime;

namespace TTSmartEcom.Api.Realtime;

internal sealed partial class SocketIoServer(
    SocketIoAuthenticator authenticator,
    SocketIoOriginPolicy originPolicy,
    IOptions<SocketIoRealtimeOptions> configuredOptions,
    ILogger<SocketIoServer> logger) : IAsyncDisposable
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ConcurrentDictionary<string, SocketIoSession> sessions = new(StringComparer.Ordinal);
    private readonly SocketIoRealtimeOptions options = configuredOptions.Value;
    private int sessionCount;
    private int disposed;

    public async Task HandleAsync(HttpContext context)
    {
        if (Volatile.Read(ref disposed) == 1)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (!originPolicy.IsAllowed(context.Request))
        {
            LogOriginRejected(logger);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        AddCorsHeaders(context);
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        if (!HasSingleQueryValue(context.Request, "EIO", "4"))
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Unsupported protocol version");
            return;
        }

        string? transport = SingleQueryValue(context.Request, "transport");
        switch (transport)
        {
            case "polling":
                await HandlePollingAsync(context);
                break;
            case "websocket" when context.WebSockets.IsWebSocketRequest:
                await HandleWebSocketAsync(context);
                break;
            default:
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Unsupported transport");
                break;
        }
    }

    public async ValueTask BroadcastAsync(
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || Volatile.Read(ref disposed) == 1)
        {
            return;
        }

        string packet = SocketIoProtocol.Event(eventName, payload);
        SocketIoSession[] targets = sessions.Values
            .Where(static session => session.IsNamespaceConnected
                && session.Authentication.IsAuthorized
                && !session.IsClosed)
            .ToArray();

        foreach (SocketIoSession session in targets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await SendAsync(session, packet, cancellationToken);
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
            {
                LogDeliveryFailed(logger);
                CloseSession(session);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        foreach (SocketIoSession session in sessions.Values)
        {
            CloseSession(session);
        }

        return ValueTask.CompletedTask;
    }

    private async Task HandlePollingAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method))
        {
            await PollingGetAsync(context);
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method))
        {
            await PollingPostAsync(context);
            return;
        }

        await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid polling method");
    }

    private async Task PollingGetAsync(HttpContext context)
    {
        string? sid = SingleQueryValue(context.Request, "sid");
        if (sid is null)
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid handshake");
                return;
            }

            SocketIoSession? created = await TryCreateSessionAsync(context);
            if (created is null)
            {
                await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "Session capacity reached");
                return;
            }

            context.Response.ContentType = "text/plain; charset=UTF-8";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync(
                SocketIoProtocol.Open(created.EngineId, canUpgrade: true, options),
                context.RequestAborted);
            return;
        }

        if (!TryGetBoundSession(context, sid, out SocketIoSession? session)
            || session is null
            || session.IsUpgraded
            || !session.TryBeginPoll())
        {
            if (session is not null && !session.IsUpgraded)
            {
                CloseSession(session);
            }

            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid session");
            return;
        }

        try
        {
            context.Response.ContentType = "text/plain; charset=UTF-8";
            context.Response.Headers.CacheControl = "no-store";
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                session.Lifetime.Token);
            SocketIoOutgoingPacket first = await session.ReadAsync(linked.Token);
            await context.Response.WriteAsync(first.Text, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested || session.IsClosed)
        {
        }
        finally
        {
            session.EndPoll();
        }
    }

    private async Task PollingPostAsync(HttpContext context)
    {
        string? sid = SingleQueryValue(context.Request, "sid");
        SocketIoSession? session = null;
        if (sid is null
            || !TryGetBoundSession(context, sid, out session)
            || session is null
            || session.IsUpgraded
            || !session.TryBeginPost())
        {
            if (session is not null && !session.IsUpgraded)
            {
                CloseSession(session);
            }

            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid session");
            return;
        }

        try
        {
            byte[]? payload = await ReadBoundedBodyAsync(context.Request, context.RequestAborted);
            if (payload is null)
            {
                CloseSession(session);
                await WriteErrorAsync(context, StatusCodes.Status413PayloadTooLarge, "Payload too large");
                return;
            }

            string text;
            try
            {
                text = StrictUtf8.GetString(payload);
            }
            catch (DecoderFallbackException)
            {
                CloseSession(session);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid UTF-8 payload");
                return;
            }

            string[] packets = text.Split(SocketIoProtocol.RecordSeparator);
            if (packets.Length == 0 || packets.Length > options.MaxPacketsPerPayload)
            {
                CloseSession(session);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid packet payload");
                return;
            }

            foreach (string packet in packets)
            {
                if (packet.Length == 0 || !await ProcessPacketAsync(context, session, packet))
                {
                    CloseSession(session);
                    await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid packet");
                    return;
                }
            }

            context.Response.ContentType = "text/plain; charset=UTF-8";
            await context.Response.WriteAsync("ok", context.RequestAborted);
        }
        finally
        {
            session.EndPost();
        }
    }

    private async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string? sid = SingleQueryValue(context.Request, "sid");
        if (sid is null)
        {
            SocketIoSession? session = await TryCreateSessionAsync(context);
            if (session is null)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            session.ActiveSocket = socket;
            try
            {
                await SendWebSocketAsync(
                    session,
                    socket,
                    SocketIoProtocol.Open(session.EngineId, canUpgrade: false, options),
                    context.RequestAborted);
                await ReceiveLoopAsync(context, session, socket, isUpgradeCandidate: false);
            }
            finally
            {
                CloseSession(session);
            }

            return;
        }

        if (!TryGetBoundSession(context, sid, out SocketIoSession? existing)
            || existing is null
            || existing.ActiveSocket is not null
            || existing.IsUpgraded)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using WebSocket candidate = await context.WebSockets.AcceptWebSocketAsync();
        using CancellationTokenSource upgradeTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted,
            existing.Lifetime.Token);
        upgradeTimeout.CancelAfter(options.UpgradeTimeout);
        try
        {
            await ReceiveLoopAsync(
                context,
                existing,
                candidate,
                isUpgradeCandidate: true,
                upgradeTimeout.Token);
        }
        catch (OperationCanceledException) when (upgradeTimeout.IsCancellationRequested)
        {
            await CloseWebSocketAsync(candidate, WebSocketCloseStatus.PolicyViolation, "upgrade timeout");
        }

        if (ReferenceEquals(existing.ActiveSocket, candidate))
        {
            CloseSession(existing);
        }
    }

    private async Task ReceiveLoopAsync(
        HttpContext context,
        SocketIoSession session,
        WebSocket socket,
        bool isUpgradeCandidate,
        CancellationToken cancellationToken = default)
    {
        CancellationToken effectiveToken = cancellationToken == default
            ? context.RequestAborted
            : cancellationToken;
        byte[] rented = ArrayPool<byte>.Shared.Rent(8_192);
        using var packetBuffer = new MemoryStream();
        try
        {
            while (socket.State == WebSocketState.Open && !effectiveToken.IsCancellationRequested)
            {
                ValueWebSocketReceiveResult result = await socket.ReceiveAsync(rented.AsMemory(), effectiveToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CloseWebSocketAsync(socket, WebSocketCloseStatus.NormalClosure, null);
                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await CloseWebSocketAsync(socket, WebSocketCloseStatus.InvalidMessageType, "text packets only");
                    return;
                }

                if (packetBuffer.Length + result.Count > options.MaxPayloadBytes)
                {
                    await CloseWebSocketAsync(socket, WebSocketCloseStatus.MessageTooBig, "payload too large");
                    return;
                }

                packetBuffer.Write(rented, 0, result.Count);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                string packet;
                try
                {
                    packet = StrictUtf8.GetString(
                        packetBuffer.GetBuffer(),
                        0,
                        checked((int)packetBuffer.Length));
                }
                catch (DecoderFallbackException)
                {
                    await CloseWebSocketAsync(socket, WebSocketCloseStatus.InvalidPayloadData, "invalid UTF-8");
                    return;
                }
                finally
                {
                    packetBuffer.SetLength(0);
                }

                if (isUpgradeCandidate)
                {
                    if (packet == "2probe")
                    {
                        // Release any pending polling GET before accepting the transport switch.
                        session.TryEnqueue("6");
                        await SendWebSocketAsync(session, socket, "3probe", effectiveToken);
                        continue;
                    }

                    if (packet == "5")
                    {
                        session.ActiveSocket = socket;
                        session.MarkUpgraded();
                        isUpgradeCandidate = false;
                        continue;
                    }

                    await CloseWebSocketAsync(socket, WebSocketCloseStatus.ProtocolError, "invalid upgrade");
                    return;
                }

                if (!await ProcessPacketAsync(context, session, packet))
                {
                    await CloseWebSocketAsync(socket, WebSocketCloseStatus.ProtocolError, "invalid packet");
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (effectiveToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async Task<bool> ProcessPacketAsync(
        HttpContext context,
        SocketIoSession session,
        string packet)
    {
        if (packet == "3")
        {
            session.MarkPongReceived();
            return true;
        }

        if (packet == "1")
        {
            CloseSession(session);
            return true;
        }

        if (!packet.StartsWith('4'))
        {
            return false;
        }

        string socketPacket = packet[1..];
        if (!session.IsNamespaceConnected)
        {
            if (!SocketIoProtocol.IsDefaultNamespaceConnect(socketPacket))
            {
                return false;
            }

            SocketIoAuthenticationState refreshed = await authenticator.AuthenticateAsync(
                context,
                session.Lifetime.Token);
            if (!SocketIoProtocol.FixedTimeEquals(
                    session.Authentication.CookieFingerprint,
                    refreshed.CookieFingerprint)
                || !refreshed.IsAuthorized)
            {
                await SendAsync(session, SocketIoProtocol.UnauthorizedPacket, context.RequestAborted);
                return true;
            }

            session.RefreshAuthentication(refreshed);
            if (session.TryConnectNamespace())
            {
                session.SocketId = SocketIoProtocol.NewId();
                await SendAsync(session, SocketIoProtocol.Connect(session.SocketId), context.RequestAborted);
            }

            return true;
        }

        if (socketPacket == "1")
        {
            session.DisconnectNamespace();
            return true;
        }

        // Valid client EVENT/ACK packets are deliberately ignored: this bounded adapter only emits server events.
        return socketPacket.Length > 0 && socketPacket[0] is '2' or '3';
    }

    private async Task<SocketIoSession?> TryCreateSessionAsync(HttpContext context)
    {
        if (Interlocked.Increment(ref sessionCount) > options.MaxSessions)
        {
            Interlocked.Decrement(ref sessionCount);
            return null;
        }

        SocketIoAuthenticationState authentication;
        try
        {
            authentication = await authenticator.AuthenticateAsync(context, context.RequestAborted);
        }
        catch
        {
            Interlocked.Decrement(ref sessionCount);
            throw;
        }

        string engineId;
        do
        {
            engineId = SocketIoProtocol.NewId();
        }
        while (sessions.ContainsKey(engineId));

        string? origin = context.Request.Headers.Origin.ToString();
        var session = new SocketIoSession(
            engineId,
            context.Request.Path,
            string.IsNullOrEmpty(origin) ? null : origin,
            authentication,
            options.MaxQueuedPacketsPerSession,
            options.MaxQueuedBytesPerSession);

        if (!sessions.TryAdd(engineId, session))
        {
            session.Dispose();
            Interlocked.Decrement(ref sessionCount);
            return null;
        }

        _ = RunSessionTimersAsync(session);
        return session;
    }

    private async Task RunSessionTimersAsync(SocketIoSession session)
    {
        await Task.WhenAll(
            EnforceNamespaceConnectTimeoutAsync(session),
            RunHeartbeatAsync(session));
    }

    private async Task EnforceNamespaceConnectTimeoutAsync(SocketIoSession session)
    {
        try
        {
            await Task.Delay(options.ConnectTimeout, session.Lifetime.Token);
            if (!session.IsNamespaceConnected)
            {
                CloseSession(session);
            }
        }
        catch (OperationCanceledException) when (session.Lifetime.IsCancellationRequested)
        {
        }
    }

    private async Task RunHeartbeatAsync(SocketIoSession session)
    {
        try
        {
            while (!session.IsClosed)
            {
                await Task.Delay(options.PingInterval, session.Lifetime.Token);
                session.MarkAwaitingPong();
                await SendAsync(session, "2", session.Lifetime.Token);
                await Task.Delay(options.PingTimeout, session.Lifetime.Token);
                if (session.IsAwaitingPong)
                {
                    LogHeartbeatTimeout(logger);
                    CloseSession(session);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (session.Lifetime.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
            CloseSession(session);
        }
    }

    private async ValueTask SendAsync(
        SocketIoSession session,
        string packet,
        CancellationToken cancellationToken)
    {
        WebSocket? socket = session.ActiveSocket;
        if (socket is { State: WebSocketState.Open })
        {
            await SendWebSocketAsync(session, socket, packet, cancellationToken);
            return;
        }

        if (!session.TryEnqueue(packet))
        {
            LogQueueLimit(logger);
            CloseSession(session);
        }
    }

    private async Task SendWebSocketAsync(
        SocketIoSession session,
        WebSocket socket,
        string packet,
        CancellationToken cancellationToken)
    {
        byte[] payload = StrictUtf8.GetBytes(packet);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            session.Lifetime.Token);
        timeout.CancelAfter(options.SendTimeout);
        await session.SendLock.WaitAsync(timeout.Token);
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
            }
        }
        finally
        {
            session.SendLock.Release();
        }
    }

    private bool TryGetBoundSession(
        HttpContext context,
        string sid,
        out SocketIoSession? session)
    {
        if (!sessions.TryGetValue(sid, out session) || session.IsClosed)
        {
            return false;
        }

        byte[] requestFingerprint = SocketIoProtocol.CookieFingerprint(
            context.Request.Cookies["authToken"]);
        return SocketIoProtocol.FixedTimeEquals(
                session.Authentication.CookieFingerprint,
                requestFingerprint)
            && SocketIoOriginPolicy.IsSameOrigin(session.Origin, context.Request)
            && string.Equals(session.Path, context.Request.Path, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<byte[]?> ReadBoundedBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > 0 && request.ContentLength > options.MaxPayloadBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream(Math.Min(options.MaxPayloadBytes, 16_384));
        byte[] rented = ArrayPool<byte>.Shared.Rent(8_192);
        try
        {
            while (true)
            {
                int remaining = options.MaxPayloadBytes + 1 - checked((int)buffer.Length);
                if (remaining <= 0)
                {
                    return null;
                }

                int read = await request.Body.ReadAsync(
                    rented.AsMemory(0, Math.Min(rented.Length, remaining)),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                buffer.Write(rented, 0, read);
            }

            return buffer.Length <= options.MaxPayloadBytes ? buffer.ToArray() : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void CloseSession(SocketIoSession session)
    {
        if (!session.TryClose())
        {
            return;
        }

        sessions.TryRemove(session.EngineId, out _);
        Interlocked.Decrement(ref sessionCount);
        WebSocket? socket = session.ActiveSocket;
        if (socket is { State: WebSocketState.Open or WebSocketState.CloseReceived })
        {
            _ = CloseWebSocketAsync(socket, WebSocketCloseStatus.NormalClosure, null);
        }
    }

    private static async Task CloseWebSocketAsync(
        WebSocket socket,
        WebSocketCloseStatus status,
        string? description)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(status, description, CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
        }
    }

    private static void AddCorsHeaders(HttpContext context)
    {
        string origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
        {
            return;
        }

        context.Response.Headers.AccessControlAllowOrigin = origin;
        context.Response.Headers.AccessControlAllowCredentials = "true";
        context.Response.Headers.Append("Vary", "Origin");
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.Headers.AccessControlAllowMethods = "GET,POST,OPTIONS";
            string requestedHeaders = context.Request.Headers.AccessControlRequestHeaders.ToString();
            if (!string.IsNullOrEmpty(requestedHeaders))
            {
                context.Response.Headers.AccessControlAllowHeaders = requestedHeaders;
            }
        }
    }

    private static bool HasSingleQueryValue(HttpRequest request, string name, string expected) =>
        string.Equals(SingleQueryValue(request, name), expected, StringComparison.Ordinal);

    private static string? SingleQueryValue(HttpRequest request, string name)
    {
        Microsoft.Extensions.Primitives.StringValues values = request.Query[name];
        return values.Count == 1 && !string.IsNullOrEmpty(values[0]) ? values[0] : null;
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=UTF-8";
        await context.Response.WriteAsJsonAsync(new { code = 3, message }, context.RequestAborted);
    }

    [LoggerMessage(EventId = 4981, Level = LogLevel.Warning, Message = "Rejected Socket.IO request from an untrusted origin")]
    private static partial void LogOriginRejected(ILogger logger);

    [LoggerMessage(EventId = 4982, Level = LogLevel.Debug, Message = "Socket.IO session closed after heartbeat timeout")]
    private static partial void LogHeartbeatTimeout(ILogger logger);

    [LoggerMessage(EventId = 4983, Level = LogLevel.Warning, Message = "Socket.IO session exceeded its outbound queue limit")]
    private static partial void LogQueueLimit(ILogger logger);

    [LoggerMessage(EventId = 4984, Level = LogLevel.Debug, Message = "Socket.IO event delivery failed")]
    private static partial void LogDeliveryFailed(ILogger logger);
}
