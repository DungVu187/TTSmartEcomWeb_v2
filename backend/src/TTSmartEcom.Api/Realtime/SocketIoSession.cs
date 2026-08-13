using System.Net.WebSockets;
using System.Threading.Channels;

namespace TTSmartEcom.Api.Realtime;

internal sealed class SocketIoSession : IDisposable
{
    private readonly object queueGate = new();
    private readonly Channel<SocketIoOutgoingPacket> outgoing;
    private readonly int maxQueuedBytes;
    private int queuedBytes;
    private int namespaceConnected;
    private int activePoll;
    private int activePost;
    private int awaitingPong;
    private int upgraded;
    private int closed;

    public SocketIoSession(
        string engineId,
        string path,
        string? origin,
        SocketIoAuthenticationState authentication,
        int maxQueuedPackets,
        int maxQueuedBytes)
    {
        EngineId = engineId;
        Path = path;
        Origin = origin;
        Authentication = authentication;
        this.maxQueuedBytes = maxQueuedBytes;
        outgoing = Channel.CreateBounded<SocketIoOutgoingPacket>(new BoundedChannelOptions(maxQueuedPackets)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    }

    public string EngineId { get; }
    public string Path { get; }
    public string? Origin { get; }
    public SocketIoAuthenticationState Authentication { get; private set; }
    public string? SocketId { get; set; }
    public WebSocket? ActiveSocket { get; set; }
    public SemaphoreSlim SendLock { get; } = new(1, 1);
    public CancellationTokenSource Lifetime { get; } = new();
    public bool IsNamespaceConnected => Volatile.Read(ref namespaceConnected) == 1;
    public bool IsAwaitingPong => Volatile.Read(ref awaitingPong) == 1;
    public bool IsUpgraded => Volatile.Read(ref upgraded) == 1;
    public bool IsClosed => Volatile.Read(ref closed) == 1;

    public bool TryConnectNamespace() => Interlocked.CompareExchange(ref namespaceConnected, 1, 0) == 0;
    public void RefreshAuthentication(SocketIoAuthenticationState authentication) => Authentication = authentication;
    public void DisconnectNamespace() => Volatile.Write(ref namespaceConnected, 0);
    public void MarkAwaitingPong() => Volatile.Write(ref awaitingPong, 1);
    public void MarkPongReceived() => Volatile.Write(ref awaitingPong, 0);
    public void MarkUpgraded() => Volatile.Write(ref upgraded, 1);
    public bool TryBeginPoll() => Interlocked.CompareExchange(ref activePoll, 1, 0) == 0;
    public void EndPoll() => Volatile.Write(ref activePoll, 0);
    public bool TryBeginPost() => Interlocked.CompareExchange(ref activePost, 1, 0) == 0;
    public void EndPost() => Volatile.Write(ref activePost, 0);

    public bool TryEnqueue(string text)
    {
        int bytes = System.Text.Encoding.UTF8.GetByteCount(text);
        lock (queueGate)
        {
            if (IsClosed || bytes > maxQueuedBytes - queuedBytes)
            {
                return false;
            }

            SocketIoOutgoingPacket packet = new(text, bytes);
            if (!outgoing.Writer.TryWrite(packet))
            {
                return false;
            }

            queuedBytes += bytes;
            return true;
        }
    }

    public async ValueTask<SocketIoOutgoingPacket> ReadAsync(CancellationToken cancellationToken)
    {
        SocketIoOutgoingPacket packet = await outgoing.Reader.ReadAsync(cancellationToken);
        OnDequeued(packet);
        return packet;
    }

    public bool TryRead(out SocketIoOutgoingPacket packet)
    {
        if (!outgoing.Reader.TryRead(out packet))
        {
            return false;
        }

        OnDequeued(packet);
        return true;
    }

    public bool TryClose()
    {
        if (Interlocked.Exchange(ref closed, 1) == 1)
        {
            return false;
        }

        outgoing.Writer.TryComplete();
        Lifetime.Cancel();
        return true;
    }

    public void Dispose()
    {
        TryClose();
        Lifetime.Dispose();
        SendLock.Dispose();
    }

    private void OnDequeued(SocketIoOutgoingPacket packet)
    {
        lock (queueGate)
        {
            queuedBytes = Math.Max(0, queuedBytes - packet.ByteCount);
        }
    }
}

internal readonly record struct SocketIoOutgoingPacket(string Text, int ByteCount);

internal sealed record SocketIoAuthenticationState(
    string? UserId,
    string? Role,
    byte[] CookieFingerprint,
    bool IsAuthorized);
