using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Meesenburg.Shop.JtlApi.Services;

/// Tolerantes Lesen von JSON-Werten: fehlende oder unpassende Werte ergeben null statt einer Ausnahme.
/// Ob ein Wert fehlt, meldet die Vertragsprüfung – das Mapping selbst darf nie einen ganzen Artikel verwerfen.
public static class JsonRead
{
    public static int? Int(JsonNode? n)
    {
        var d = Dec(n);
        return d is { } v && v == decimal.Truncate(v) && v is >= int.MinValue and <= int.MaxValue ? (int)v : null;
    }

    public static decimal? Dec(JsonNode? n) => n?.GetValueKind() switch
    {
        JsonValueKind.Number => decimal.TryParse(n.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var num) ? num : null,
        JsonValueKind.String when decimal.TryParse(n.GetValue<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => null
    };

    public static string? Str(JsonNode? n) => n?.GetValueKind() switch
    {
        JsonValueKind.String => n.GetValue<string>(),
        JsonValueKind.Number => n.ToJsonString(),
        _ => null
    };

    public static bool? Bool(JsonNode? n) => n?.GetValueKind() switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => Dec(n) != 0,
        JsonValueKind.String when bool.TryParse(n.GetValue<string>(), out var b) => b,
        _ => null
    };

    public static DateTimeOffset? Date(JsonNode? n) =>
        Str(n) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    public static IEnumerable<JsonObject> Objects(JsonNode? n) => (n as JsonArray)?.OfType<JsonObject>() ?? [];
}
