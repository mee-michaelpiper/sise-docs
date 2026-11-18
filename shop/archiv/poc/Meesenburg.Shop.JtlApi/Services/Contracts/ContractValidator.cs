using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Meesenburg.Shop.JtlApi.Models;

namespace Meesenburg.Shop.JtlApi.Services.Contracts;

/// Prüft JSON gegen die Feldregeln so, wie Pydantic v2 (lax mode) im Alt-Backend validieren würde:
/// int nimmt 5 und 5.0, aber nicht 5.5; str nimmt keine Zahlen; bool nimmt true/false, 0/1, "true"/"false".
public static class ContractValidator
{
    public static IReadOnlyList<JsonObject> Records(JsonNode? body, ContractShape shape) => shape switch
    {
        ContractShape.PagedList => (body?["Items"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [],
        ContractShape.List => (body as JsonArray)?.OfType<JsonObject>().ToList() ?? [],
        _ => body is JsonObject o ? [o] : []
    };

    public static List<DataIssue> Validate(ContractDefinition contract, JsonObject record, string? recordLabel = null)
    {
        var issues = new List<DataIssue>();
        foreach (var rule in contract.Rules)
        {
            foreach (var (path, node, present) in Resolve(record, rule.Path))
            {
                var field = recordLabel is null ? path : $"{recordLabel}: {path}";
                if (!present)
                {
                    if (rule.Required)
                        issues.Add(new(contract.Key, field, IssueSeverity.Error, "Feld fehlt", contract.LegacyEffect));
                    continue;
                }

                if (node is null)
                {
                    if (!rule.Nullable)
                        issues.Add(new(contract.Key, field, IssueSeverity.Error,
                            rule.Required ? "Wert ist null" : "Wert ist null (Default greift nur, wenn das Feld fehlt)",
                            contract.LegacyEffect));
                    continue;
                }

                var problem = CheckKind(node, rule.Kind);
                if (problem is not null)
                    issues.Add(new(contract.Key, field, IssueSeverity.Error, problem, contract.LegacyEffect));
            }
        }
        return issues;
    }

    /// Löst "A.B" bzw. "A[].B" auf. Liefert je Treffer (konkreter Pfad, Wert, vorhanden?).
    /// Fehlt ein Zwischenobjekt, meldet nur dessen eigene Regel den Fehler (keine Folgefehler).
    private static IEnumerable<(string Path, JsonNode? Node, bool Present)> Resolve(JsonObject root, string rulePath)
    {
        var segments = rulePath.Split('.');
        IEnumerable<(string Path, JsonNode? Node)> current = [("", root)];

        for (var i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            var isArray = seg.EndsWith("[]", StringComparison.Ordinal);
            var name = isArray ? seg[..^2] : seg;
            var last = i == segments.Length - 1;
            var next = new List<(string, JsonNode?)>();

            foreach (var (path, node) in current)
            {
                if (node is not JsonObject obj) continue; // Elternteil fehlt/ist null – meldet eigene Regel
                var childPath = path.Length == 0 ? name : $"{path}.{name}";

                if (!obj.TryGetPropertyValue(name, out var child))
                {
                    if (last) yield return (childPath, null, false);
                    continue;
                }

                if (last && !isArray)
                {
                    yield return (childPath, child, true);
                    continue;
                }

                if (isArray)
                {
                    if (child is JsonArray arr)
                        for (var j = 0; j < arr.Count; j++)
                            next.Add(($"{childPath}[{j}]", arr[j]));
                }
                else
                {
                    next.Add((childPath, child));
                }
            }

            current = next;
            if (last && isArray)
                foreach (var (p, n) in current)
                    yield return (p, n, true);
        }
    }

    private static string? CheckKind(JsonNode node, JsonKind kind)
    {
        var valueKind = node.GetValueKind();
        switch (kind)
        {
            case JsonKind.Array:
                return valueKind == JsonValueKind.Array ? null : $"Array erwartet, {Describe(node)} erhalten";
            case JsonKind.Object:
                return valueKind == JsonValueKind.Object ? null : $"Objekt erwartet, {Describe(node)} erhalten";
            case JsonKind.String:
                return valueKind == JsonValueKind.String ? null : $"Text erwartet, {Describe(node)} erhalten";
            case JsonKind.Bool:
                if (valueKind is JsonValueKind.True or JsonValueKind.False) return null;
                if (valueKind == JsonValueKind.Number && JsonRead.Dec(node) is 0 or 1) return null;
                if (valueKind == JsonValueKind.String && bool.TryParse(node.GetValue<string>(), out _)) return null;
                return $"Wahrheitswert erwartet, {Describe(node)} erhalten";
            case JsonKind.Number:
                if (valueKind == JsonValueKind.Number) return null;
                if (valueKind == JsonValueKind.String && decimal.TryParse(node.GetValue<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return null;
                return $"Zahl erwartet, {Describe(node)} erhalten";
            case JsonKind.Int:
                if (valueKind == JsonValueKind.Number)
                {
                    var d = JsonRead.Dec(node)!.Value;
                    return d == decimal.Truncate(d) ? null : $"Ganzzahl erwartet, {d.ToString(CultureInfo.InvariantCulture)} erhalten (Nachkommastellen)";
                }
                if (valueKind == JsonValueKind.String && long.TryParse(node.GetValue<string>(), out _)) return null;
                return $"Ganzzahl erwartet, {Describe(node)} erhalten";
            case JsonKind.DateTime:
                if (valueKind == JsonValueKind.String && DateTimeOffset.TryParse(node.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)) return null;
                if (valueKind == JsonValueKind.Number) return null;
                return $"Datum erwartet, {Describe(node)} erhalten";
            default:
                return null;
        }
    }

    private static string Describe(JsonNode node) => node.GetValueKind() switch
    {
        JsonValueKind.String => $"Text \"{Trim(node.GetValue<string>())}\"",
        JsonValueKind.Number => $"Zahl {node.ToJsonString()}",
        JsonValueKind.True or JsonValueKind.False => $"Wahrheitswert {node.ToJsonString()}",
        JsonValueKind.Array => "Array",
        JsonValueKind.Object => "Objekt",
        _ => node.ToJsonString()
    };

    private static string Trim(string s) => s.Length <= 40 ? s : s[..40] + "…";
}
