using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Meesenburg.Shop.JtlApi.Brokers.Jtl;

/// Gemeinsame HTTP-Logik für JTL-Wawi und Custom-API: liefert immer Status, Dauer und Body – nie ein stilles null.
public abstract class JsonBroker(HttpClient httpClient, ILogger logger)
{
    protected abstract string Source { get; }

    public async ValueTask<JtlResponse> GetAsync(string relativePath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await httpClient.GetAsync(relativePath.TrimStart('/'), ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();

            JsonNode? body = null;
            string? error = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try { body = JsonNode.Parse(text); }
                catch (JsonException) { error = "Antwort ist kein JSON: " + Truncate(text, 300); }
            }

            if (!response.IsSuccessStatusCode)
                error ??= Truncate(text, 500);

            if (error is not null)
                logger.LogWarning("{Source} GET {Path} -> {Status} in {Ms} ms: {Error}",
                    Source, relativePath, (int)response.StatusCode, sw.ElapsedMilliseconds, error);
            else
                logger.LogDebug("{Source} GET {Path} -> {Status} in {Ms} ms",
                    Source, relativePath, (int)response.StatusCode, sw.ElapsedMilliseconds);

            return new JtlResponse(Source, relativePath, (int)response.StatusCode, sw.Elapsed, body, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            sw.Stop();
            logger.LogError(ex, "{Source} GET {Path} fehlgeschlagen", Source, relativePath);
            return new JtlResponse(Source, relativePath, 0, sw.Elapsed, null, ex.Message);
        }
    }

    /// Wie GetAsync, wirft aber bei Fehlern (für Abfragen, bei denen ein Teilergebnis falsch wäre).
    public async ValueTask<JsonNode> GetRequiredAsync(string relativePath, CancellationToken ct = default)
    {
        var response = await GetAsync(relativePath, ct);
        if (!response.IsSuccess || response.Body is null)
            throw new JtlRequestException(response);
        return response.Body;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}

public interface IJtlWawiBroker
{
    ValueTask<JtlResponse> GetAsync(string relativePath, CancellationToken ct = default);
    ValueTask<JsonNode> GetRequiredAsync(string relativePath, CancellationToken ct = default);
}

public interface ICustomApiBroker
{
    ValueTask<JtlResponse> GetAsync(string relativePath, CancellationToken ct = default);
    ValueTask<JsonNode> GetRequiredAsync(string relativePath, CancellationToken ct = default);
}

public sealed class JtlWawiBroker(HttpClient httpClient, ILogger<JtlWawiBroker> logger)
    : JsonBroker(httpClient, logger), IJtlWawiBroker
{
    protected override string Source => "JTL-Wawi";
}

public sealed class CustomApiBroker(HttpClient httpClient, ILogger<CustomApiBroker> logger)
    : JsonBroker(httpClient, logger), ICustomApiBroker
{
    protected override string Source => "Custom-API";
}
