using Meesenburg.Shop.JtlApi.Brokers.Jtl;
using Meesenburg.Shop.JtlApi.Models;
using Meesenburg.Shop.JtlApi.Services;
using Meesenburg.Shop.JtlApi.Services.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Meesenburg.Shop.JtlApi.Apis;

public static class ArticleApi
{
    public static IEndpointRouteBuilder MapArticleApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/jtl/articles").WithTags("Artikel");

        api.MapGet("", GetArticles).WithSummary("Artikel aus der JTL-Wawi (Suche: Text oder Artikel-Id)");
        api.MapGet("/{id:int}", GetArticle).WithSummary("Artikel mit Preisen, Varianten, Bildern und Prüfergebnis");
        api.MapGet("/{id:int}/raw", GetRaw).WithSummary("Unveränderte JTL-Antwort (part = item, images, specialprices, salesChannelPrices, variations, properties, descriptions, customfields, suppliers)");

        return app;
    }

    public static async Task<Ok<ArticlePage>> GetArticles(
        [FromServices] IArticleService service, string? search, int page = 1, int pageSize = 25, CancellationToken ct = default)
        => TypedResults.Ok(await service.RetrieveArticlesAsync(search, page, pageSize, ct));

    public static async Task<Results<Ok<ArticleDetail>, NotFound>> GetArticle(
        int id, [FromServices] IArticleService service, CancellationToken ct = default)
        => await service.RetrieveArticleAsync(id, ct) is { } article ? TypedResults.Ok(article) : TypedResults.NotFound();

    public static async Task<IResult> GetRaw(
        int id, [FromServices] IArticleService service, string? part, CancellationToken ct = default)
    {
        var r = await service.RetrieveRawAsync(id, part, ct);
        return Results.Json(new { r.Source, r.Path, r.StatusCode, ElapsedMs = (long)r.Elapsed.TotalMilliseconds, r.ErrorText, r.Body });
    }
}

public static class ContractApi
{
    public static IEndpointRouteBuilder MapContractApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/jtl/contract").WithTags("Schnittstellen-Prüfung");

        api.MapGet("/check", Check).WithSummary("JTL-Antworten gegen die Erwartungen des Alt-Backends und den letzten Stand prüfen");
        api.MapGet("/snapshots", ListSnapshots).WithSummary("Gespeicherte Schnittstellen-Stände");
        api.MapPost("/snapshots", CreateSnapshot).WithSummary("Aktuellen Stand der Schnittstelle speichern (nur Struktur, keine Werte)");

        return app;
    }

    public static async Task<Ok<ContractReport>> Check(
        [FromServices] IContractCheckService service, string? itemIds, string? snapshot, CancellationToken ct = default)
        => TypedResults.Ok(await service.CheckAsync(ParseIds(itemIds), snapshot, ct));

    public static async Task<Ok<IReadOnlyList<SnapshotInfo>>> ListSnapshots(
        [FromServices] IContractCheckService service, CancellationToken ct = default)
        => TypedResults.Ok(await service.ListSnapshotsAsync(ct));

    public static async Task<Created<SnapshotInfo>> CreateSnapshot(
        [FromServices] IContractCheckService service, string? itemIds, CancellationToken ct = default)
    {
        var info = await service.CreateSnapshotAsync(ParseIds(itemIds), ct);
        return TypedResults.Created($"/jtl/contract/snapshots/{info.Name}", info);
    }

    /// Höchstens 20 Artikel je Prüfung – je Artikel gehen 8–16 Abfragen an JTL
    private static IReadOnlyList<int> ParseIds(string? ids) =>
        (ids ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var v) ? v : (int?)null).OfType<int>().Distinct().Take(20).ToList();
}

public static class StatusApi
{
    public static IEndpointRouteBuilder MapStatusApiV1(this IEndpointRouteBuilder app)
    {
        app.MapGet("/jtl/status", async ([FromServices] IConnectionService service, CancellationToken ct)
                => TypedResults.Ok(await service.CheckAsync(ct)))
            .WithTags("Verbindung").WithSummary("Erreichbarkeit von JTL-Wawi und Custom-API");
        return app;
    }
}

/// JTL-Fehler als ProblemDetails mit Pfad und Status – nie als leeres Ergebnis.
public sealed class JtlExceptionHandler(IProblemDetailsService problemDetails) : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not JtlRequestException jtl) return false;

        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "JTL-Wawi hat nicht wie erwartet geantwortet",
                Detail = jtl.Response.StatusCode == 0
                    ? $"Keine Verbindung zu {jtl.Response.Source} ({jtl.Response.ErrorText})"
                    : $"{jtl.Response.Source} {jtl.Response.Path} → {jtl.Response.StatusCode}: {jtl.Response.ErrorText}",
                Extensions = { ["source"] = jtl.Response.Source, ["path"] = jtl.Response.Path, ["upstreamStatus"] = jtl.Response.StatusCode }
            }
        });
    }
}
