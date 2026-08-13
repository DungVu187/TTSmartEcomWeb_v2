using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TTSmartEcom.Api.Realtime;

internal static class SocketIoProtocol
{
    public const char RecordSeparator = '\u001e';
    public const string UnauthorizedPacket = "44{\"message\":\"unauthorized\"}";
    private static readonly string[] WebSocketUpgrade = ["websocket"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Open(
        string engineId,
        bool canUpgrade,
        SocketIoRealtimeOptions options) => "0" + JsonSerializer.Serialize(new
        {
            sid = engineId,
            upgrades = canUpgrade ? WebSocketUpgrade : [],
            pingInterval = options.PingIntervalMilliseconds,
            pingTimeout = options.PingTimeoutMilliseconds,
            maxPayload = options.MaxPayloadBytes,
        }, JsonOptions);

    public static string Connect(string socketId) =>
        "40" + JsonSerializer.Serialize(new { sid = socketId }, JsonOptions);

    public static string Event(string eventName, object payload) =>
        "42" + JsonSerializer.Serialize(new object[] { eventName, payload }, JsonOptions);

    public static string NewId() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(15))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public static byte[] CookieFingerprint(string? cookieValue) => string.IsNullOrEmpty(cookieValue)
        ? []
        : SHA256.HashData(Encoding.UTF8.GetBytes(cookieValue));

    public static bool FixedTimeEquals(byte[] left, byte[] right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);

    public static bool IsDefaultNamespaceConnect(string packet)
    {
        if (packet == "0")
        {
            return true;
        }

        if (!packet.StartsWith("0{", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using JsonDocument payload = JsonDocument.Parse(packet[1..]);
            return payload.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
