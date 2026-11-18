using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Meesenburg.Shop.JtlApi.Models;
using MudBlazor;

namespace Meesenburg.Shop.Portal.Services;

public sealed record RawResult(string Source, string Path, int StatusCode, long ElapsedMs, string? ErrorText, JsonNode? Body);

/// Zugriff auf die Shop-JTL-API (Broker → Seite; im POC ohne eigene Service-Schicht, weil nichts gemappt wird).
public sealed class JtlApiBroker(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<ArticlePage> GetArticlesAsync(string? search, int page, int pageSize, CancellationToken ct = default) =>
        GetAsync<ArticlePage>($"jtl/articles?page={page}&pageSize={pageSize}&search={Uri.EscapeDataString(search ?? "")}", ct)!;

    public async Task<ArticleDetail?> GetArticleAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"jtl/articles/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ArticleDetail>(Json, ct);
    }

    public Task<RawResult> GetRawAsync(int id, string part, CancellationToken ct = default) =>
        GetAsync<RawResult>($"jtl/articles/{id}/raw?part={Uri.EscapeDataString(part)}", ct)!;

    public Task<ContractReport> CheckContractAsync(IEnumerable<int> itemIds, string? snapshot, CancellationToken ct = default) =>
        GetAsync<ContractReport>($"jtl/contract/check?itemIds={string.Join(',', itemIds)}{(snapshot is null ? "" : "&snapshot=" + Uri.EscapeDataString(snapshot))}", ct)!;

    public Task<List<SnapshotInfo>> GetSnapshotsAsync(CancellationToken ct = default) =>
        GetAsync<List<SnapshotInfo>>("jtl/contract/snapshots", ct)!;

    public async Task<SnapshotInfo> CreateSnapshotAsync(IEnumerable<int> itemIds, CancellationToken ct = default)
    {
        using var response = await http.PostAsync($"jtl/contract/snapshots?itemIds={string.Join(',', itemIds)}", null, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<SnapshotInfo>(Json, ct))!;
    }

    public Task<ConnectionStatus> GetStatusAsync(CancellationToken ct = default) =>
        GetAsync<ConnectionStatus>("jtl/status", ct)!;

    public Task<T?> GetCustomAsync<T>(string key, CancellationToken ct = default) =>
        GetAsync<T>($"custom/{Uri.EscapeDataString(key)}", ct);

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    /// Fehler der API (ProblemDetails) als lesbare Meldung weitergeben
    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? detail = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
            detail = (string?)problem?["detail"] ?? (string?)problem?["title"];
        }
        catch (JsonException) { }
        throw new HttpRequestException(detail ?? $"Shop-API antwortete mit {(int)response.StatusCode}", null, response.StatusCode);
    }
}

/// Zentrale Anzeige-Helfer (Status → Farbe/Text/Icon), wie JobUi im SiSe Portal.
public static class JtlUi
{
    public static Color StatusColor(ContractStatus s) => s switch
    {
        ContractStatus.Ok => Color.Success,
        ContractStatus.Changed => Color.Warning,
        _ => Color.Error
    };

    public static string StatusText(ContractStatus s) => s switch
    {
        ContractStatus.Ok => "In Ordnung",
        ContractStatus.Changed => "Geändert",
        _ => "Bricht Alt-Backend"
    };

    public static Color SeverityColor(IssueSeverity s) => s switch
    {
        IssueSeverity.Error => Color.Error,
        IssueSeverity.Warning => Color.Warning,
        _ => Color.Info
    };

    public static string SeverityText(IssueSeverity s) => s switch
    {
        IssueSeverity.Error => "Fehler",
        IssueSeverity.Warning => "Warnung",
        _ => "Info"
    };

    public static string ChangeText(SchemaChangeKind k) => k switch
    {
        SchemaChangeKind.Added => "neu",
        SchemaChangeKind.Removed => "entfernt",
        _ => "Typ geändert"
    };

    public static Color HttpColor(int status) => status switch
    {
        >= 200 and < 300 => Color.Success,
        404 => Color.Default,
        0 => Color.Error,
        _ => Color.Error
    };

    public static string HttpText(int status) => status == 0 ? "keine Verbindung" : status.ToString();

    private static readonly System.Globalization.CultureInfo De = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public static string Euro(decimal? v) => v is null ? "–" : v.Value.ToString("#,##0.00 €", De);
    public static string Num(decimal? v) => v is null ? "–" : v.Value.ToString("#,##0.##", De);

    /// Anzeige immer in deutscher Zeit (Container laufen in UTC) – wie PortalTime im SiSe Portal
    public static string Local(DateTimeOffset? utc) =>
        utc is null ? "–" : TimeZoneInfo.ConvertTime(utc.Value, Berlin).ToString("dd.MM.yyyy HH:mm", De);

    public static DateTime Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Berlin).DateTime;
}
