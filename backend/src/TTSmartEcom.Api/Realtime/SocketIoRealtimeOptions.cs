using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Api.Realtime;

public sealed class SocketIoRealtimeOptions
{
    public const string SectionName = "Realtime:SocketIo";

    [Range(50, 120_000)]
    public int PingIntervalMilliseconds { get; init; } = 25_000;

    [Range(50, 120_000)]
    public int PingTimeoutMilliseconds { get; init; } = 20_000;

    [Range(100, 120_000)]
    public int ConnectTimeoutMilliseconds { get; init; } = 45_000;

    [Range(100, 60_000)]
    public int UpgradeTimeoutMilliseconds { get; init; } = 10_000;

    [Range(100, 30_000)]
    public int SendTimeoutMilliseconds { get; init; } = 5_000;

    [Range(128, 1_048_576)]
    public int MaxPayloadBytes { get; init; } = 1_000_000;

    [Range(1, 256)]
    public int MaxPacketsPerPayload { get; init; } = 64;

    [Range(1, 4_096)]
    public int MaxQueuedPacketsPerSession { get; init; } = 128;

    [Range(1_024, 67_108_864)]
    public int MaxQueuedBytesPerSession { get; init; } = 4_000_000;

    [Range(1, 100_000)]
    public int MaxSessions { get; init; } = 2_048;

    internal TimeSpan PingInterval => TimeSpan.FromMilliseconds(PingIntervalMilliseconds);
    internal TimeSpan PingTimeout => TimeSpan.FromMilliseconds(PingTimeoutMilliseconds);
    internal TimeSpan ConnectTimeout => TimeSpan.FromMilliseconds(ConnectTimeoutMilliseconds);
    internal TimeSpan UpgradeTimeout => TimeSpan.FromMilliseconds(UpgradeTimeoutMilliseconds);
    internal TimeSpan SendTimeout => TimeSpan.FromMilliseconds(SendTimeoutMilliseconds);
}
