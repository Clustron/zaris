using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveBetting.Core.Infrastructure;

/// <summary>
/// The single UTF-8 JSON codec used for every document, pub/sub payload and stream entry in the
/// solution. Enums serialize as strings (so audit streams and settlement logs stay human-readable),
/// and output is compact. Money is carried as whole minor units (<c>long</c>) everywhere, never as a
/// floating-point value, so serialization can never introduce rounding error.
/// </summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static byte[] Bytes<T>(T value) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Options));

    public static T? From<T>(byte[] payload) =>
        payload is { Length: > 0 } ? JsonSerializer.Deserialize<T>(payload, Options) : default;

    public static T? From<T>(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<T>(payload, Options);
}
