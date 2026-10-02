using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeaderboardPresence.Core;

/// <summary>UTF-8 JSON codec for event payloads carried over Zaris pub/sub and streams.</summary>
internal static class Json
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static byte[] Bytes<T>(T value) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Opts));

    public static T? From<T>(byte[] payload) => JsonSerializer.Deserialize<T>(payload, Opts);

    public static T? From<T>(ReadOnlySpan<byte> payload) => JsonSerializer.Deserialize<T>(payload, Opts);
}

/// <summary>Stored per-player presence record (behind <see cref="Keys.PresenceKey"/>).</summary>
internal sealed class PresenceRecord
{
    public long LastSeenMs { get; set; }
    public string? Detail { get; set; }
    /// <summary>User-initiated override (e.g. manual Away). Honoured while at least as recent as the last heartbeat.</summary>
    public PresenceStatus? ExplicitStatus { get; set; }
    public long ExplicitAtMs { get; set; }
}
