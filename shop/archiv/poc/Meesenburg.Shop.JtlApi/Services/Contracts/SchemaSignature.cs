using System.Text.Json;
using System.Text.Json.Nodes;
using Meesenburg.Shop.JtlApi.Models;

namespace Meesenburg.Shop.JtlApi.Services.Contracts;

/// Struktur einer Antwort ohne Werte: Feldpfad → beobachtete Typen (z. B. "ItemPriceData.SalesPriceNet" → "number").
/// Zwei Signaturen lassen sich vergleichen, um Änderungen an der Schnittstelle zu belegen.
public static class SchemaSignature
{
    public static SortedDictionary<string, SortedSet<string>> Build(IEnumerable<JsonNode?> records)
    {
        var result = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var record in records)
            Walk(record, "", result);
        return result;
    }

    private static void Walk(JsonNode? node, string path, SortedDictionary<string, SortedSet<string>> acc)
    {
        var kind = node is null ? "null" : node.GetValueKind() switch
        {
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => node.ToJsonString().Contains('.') ? "number" : "integer",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            _ => "null"
        };

        if (path.Length > 0)
        {
            if (!acc.TryGetValue(path, out var kinds))
                acc[path] = kinds = new SortedSet<string>(StringComparer.Ordinal);
            kinds.Add(kind);
        }

        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, child) in obj)
                    Walk(child, path.Length == 0 ? name : $"{path}.{name}", acc);
                break;
            case JsonArray arr:
                foreach (var child in arr)
                    Walk(child, $"{path}[]", acc);
                break;
        }
    }

    /// Vergleich zweier Signaturen. "integer" → "number" gilt nicht als Änderung (Zahl ohne Nachkommastellen).
    public static List<SchemaChange> Diff(
        IReadOnlyDictionary<string, SortedSet<string>> before,
        IReadOnlyDictionary<string, SortedSet<string>> after)
    {
        var changes = new List<SchemaChange>();
        foreach (var (path, kinds) in after)
        {
            if (!before.TryGetValue(path, out var old))
                changes.Add(new(path, SchemaChangeKind.Added, null, string.Join("|", kinds)));
            else if (!Normalize(old).SetEquals(Normalize(kinds)))
                changes.Add(new(path, SchemaChangeKind.TypeChanged, string.Join("|", old), string.Join("|", kinds)));
        }
        foreach (var (path, kinds) in before)
            if (!after.ContainsKey(path))
                changes.Add(new(path, SchemaChangeKind.Removed, string.Join("|", kinds), null));
        return changes.OrderBy(c => c.Field, StringComparer.Ordinal).ToList();
    }

    private static HashSet<string> Normalize(IEnumerable<string> kinds) =>
        kinds.Select(k => k == "integer" ? "number" : k).ToHashSet();
}
