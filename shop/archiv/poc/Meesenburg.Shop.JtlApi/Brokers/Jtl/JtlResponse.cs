using System.Text.Json.Nodes;

namespace Meesenburg.Shop.JtlApi.Brokers.Jtl;

/// Rohantwort eines Aufrufs – bewusst inkl. Status und Dauer, damit Fehler nie still verschwinden
/// (das Alt-Backend wertet den HTTP-Status gar nicht aus und verschluckt Validierungsfehler).
public sealed record JtlResponse(
    string Source,
    string Path,
    int StatusCode,
    TimeSpan Elapsed,
    JsonNode? Body,
    string? ErrorText)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300 && ErrorText is null;
}

public sealed class JtlRequestException(JtlResponse response)
    : Exception($"{response.Source} {response.Path} antwortete mit {response.StatusCode}: {response.ErrorText}")
{
    public JtlResponse Response { get; } = response;
}
