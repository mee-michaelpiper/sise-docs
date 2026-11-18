using System.Text.Json.Nodes;
using Meesenburg.Shop.JtlApi.Brokers.Jtl;
using Meesenburg.Shop.JtlApi.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Meesenburg.Shop.JtlApi.Apis;

/// Lesender Zugriff auf die Custom-API des Alt-Dienstleisters (direkter SQL-Zugriff auf die JTL-Datenbank).
/// Nur eine feste Liste von GET-Pfaden – die schreibenden Endpunkte (z. B. POST /payment) sind bewusst nicht erreichbar.
public static class CustomApiApi
{
    public sealed record CustomResource(string Key, string Path, string Title, string Description);

    public static readonly CustomResource[] Resources =
    [
        new("config_groups", "config_groups", "Konfigurationsgruppen", "Gruppen des Schließanlagen-Konfigurators (Min/Max, Typ, Sortierung je Artikel)"),
        new("config_items", "config_items", "Konfig-Artikel", "Artikel je Konfig-Gruppe inkl. Preis je Kundengruppe (fPreis, bPreis, bRabatt, bZuschlag)"),
        new("property_values", "property_values", "Variationswerte", "Aufpreise (fAufpreis/fAufpreisNetto), Gewicht, Lagerbestand, Barcode je Variationswert"),
        new("categories", "categories", "Kategorien", "Kategoriebaum inkl. Rabatte je Kundengruppe und SEO"),
        new("shipping_methods", "shipping_methods", "Versandarten", "Versandarten mit Bruttopreis und Versandkostenfrei-Grenze"),
        new("child_item_mappings", "child_item_mappings", "Kind-Artikel-Zuordnung", "Vater/Kind-Artikel und Variationskombinationen"),
        new("item_units", "item-units", "Artikeleinheiten", "Einheit und Gewicht je Artikel"),
    ];

    public static IEndpointRouteBuilder MapCustomApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/custom").WithTags("Custom-API (lesend)");

        api.MapGet("/resources", () => TypedResults.Ok(Resources)).WithSummary("Verfügbare lesende Ressourcen");
        api.MapGet("/{key}", Get).WithSummary("Unveränderte Antwort der Custom-API (nur freigegebene GET-Pfade)");

        return app;
    }

    public static async Task<IResult> Get(
        string key,
        [FromServices] ICustomApiBroker broker,
        [FromServices] IOptions<CustomApiOptions> options,
        [FromServices] SampleDataInfo sample,
        CancellationToken ct)
    {
        var resource = Resources.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (resource is null)
            return Results.Problem(statusCode: 404, title: "Unbekannte Ressource",
                detail: $"Erlaubt: {string.Join(", ", Resources.Select(r => r.Key))}");

        if (!options.Value.IsConfigured)
            return Results.Problem(statusCode: 503, title: "Custom-API nicht konfiguriert",
                detail: sample.Enabled
                    ? "Für die Custom-API gibt es keine Beispieldaten. CustomApi:BaseUrl und CustomApi:ApiToken setzen."
                    : "CustomApi:BaseUrl und CustomApi:ApiToken setzen (User-Secrets, Umgebung oder Key Vault).");

        var r = await broker.GetAsync(resource.Path, ct);
        var count = r.Body switch { JsonArray a => a.Count, JsonObject o => o.Count, _ => 0 };
        return Results.Json(new { r.Source, r.Path, r.StatusCode, ElapsedMs = (long)r.Elapsed.TotalMilliseconds, r.ErrorText, Records = count, r.Body });
    }
}
