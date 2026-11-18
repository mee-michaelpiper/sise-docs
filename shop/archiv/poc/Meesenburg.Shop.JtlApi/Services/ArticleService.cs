using System.Text.Json.Nodes;
using Meesenburg.Shop.JtlApi.Brokers.Jtl;
using Meesenburg.Shop.JtlApi.Models;
using Meesenburg.Shop.JtlApi.Options;
using Meesenburg.Shop.JtlApi.Services.Contracts;
using Microsoft.Extensions.Options;

namespace Meesenburg.Shop.JtlApi.Services;

public interface IArticleService
{
    ValueTask<ArticlePage> RetrieveArticlesAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    ValueTask<ArticleDetail?> RetrieveArticleAsync(int id, CancellationToken ct = default);
    ValueTask<JtlResponse> RetrieveRawAsync(int id, string? part, CancellationToken ct = default);
}

public sealed class ArticleService(
    IJtlWawiBroker jtl,
    ITaxService tax,
    IOptions<JtlSearchOptions> searchOptions,
    IOptions<JtlOptions> jtlOptions,
    SampleDataInfo sampleData,
    ILogger<ArticleService> logger) : IArticleService
{
    private const decimal LegacyFallbackTaxPercent = 1m; // global_data_logic.py:123

    private static readonly Dictionary<string, string> Parts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["item"] = "v1/items/{id}",
        ["images"] = "v1/items/{id}/images",
        ["specialprices"] = "v1/items/{id}/specialprices",
        ["salesChannelPrices"] = "v1/items/{id}/salesChannelPrices",
        ["variations"] = "v1/items/{id}/variations",
        ["properties"] = "v1/items/{id}/properties",
        ["descriptions"] = "v1/items/{id}/descriptions",
        ["customfields"] = "v1/items/{id}/customfields",
        ["suppliers"] = "v1/items/{id}/suppliers",
    };

    public async ValueTask<ArticlePage> RetrieveArticlesAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Reine Zahl = Artikel-Id direkt abfragen
        if (int.TryParse(search, out var id))
        {
            var single = await jtl.GetAsync($"v1/items/{id}", ct);
            var items = single is { IsSuccess: true, Body: JsonObject obj } ? new[] { MapListItem(obj) } : [];
            return new ArticlePage(1, pageSize, items.Length, 1, items, sampleData.Enabled);
        }

        var query = $"v1/items?pageNumber={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(jtlOptions.Value.SalesChannelId))
            query += $"&isActiveOnSalesChannelId={Uri.EscapeDataString(jtlOptions.Value.SalesChannelId)}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&{searchOptions.Value.Parameter}={Uri.EscapeDataString(search.Trim())}";

        var body = await jtl.GetRequiredAsync(query, ct);
        var list = JsonRead.Objects(body["Items"]).Select(MapListItem).ToList();

        return new ArticlePage(
            JsonRead.Int(body["PageNumber"]) ?? page,
            JsonRead.Int(body["PageSize"]) ?? pageSize,
            JsonRead.Int(body["TotalItems"]) ?? list.Count,
            JsonRead.Int(body["TotalPages"]) ?? 1,
            list,
            sampleData.Enabled);
    }

    public async ValueTask<ArticleDetail?> RetrieveArticleAsync(int id, CancellationToken ct = default)
    {
        var itemResponse = await jtl.GetAsync($"v1/items/{id}", ct);
        if (itemResponse.StatusCode == 404) return null;
        if (!itemResponse.IsSuccess || itemResponse.Body is not JsonObject item)
            throw new JtlRequestException(itemResponse);

        // Unterressourcen parallel – jede Antwort wird einzeln bewertet, ein Fehler verwirft nichts anderes
        var parts = new[] { "images", "specialprices", "salesChannelPrices", "variations" };
        var responses = await Task.WhenAll(parts.Select(p => jtl.GetAsync(Parts[p].Replace("{id}", id.ToString()), ct).AsTask()));
        var byPart = parts.Zip(responses).ToDictionary(x => x.First, x => x.Second, StringComparer.OrdinalIgnoreCase);

        var issues = new List<DataIssue>();
        issues.AddRange(ContractValidator.Validate(LegacyContracts.Items, item));
        if (JsonRead.Objects(item["Categories"]).Any() is false)
            issues.Add(new("items", "Categories", IssueSeverity.Warning, "Artikel ist keiner Kategorie zugeordnet",
                "Das Alt-Backend lädt Artikel nur über Kategorien – dieser Artikel wird nie in den Shop übernommen."));
        AddPartIssues(issues, LegacyContracts.Images, byPart["images"]);
        AddPartIssues(issues, LegacyContracts.SpecialPrices, byPart["specialprices"]);
        AddPartIssues(issues, LegacyContracts.SalesChannelPrices, byPart["salesChannelPrices"]);
        AddPartIssues(issues, LegacyContracts.Variations, byPart["variations"]);

        // Preise: Brutto hier und nur hier
        var taxClassId = JsonRead.Int(item["TaxClassId"]);
        var taxResult = taxClassId is null ? new TaxRateResult(null, "Artikel ohne Steuerklasse") : await tax.GetRateAsync(taxClassId.Value, ct);
        if (taxResult.RatePercent is null)
            issues.Add(new("taxRate", $"Steuerklasse {taxClassId}", IssueSeverity.Error, $"Steuersatz {taxResult.Source}",
                LegacyContracts.TaxRate.LegacyEffect));

        var priceData = item["ItemPriceData"];
        var net = JsonRead.Dec(priceData?["SalesPriceNet"]);
        var price = new ArticlePrice(
            net,
            JsonRead.Dec(priceData?["SuggestedRetailPrice"]),
            JsonRead.Dec(priceData?["PurchasePriceNet"]),
            taxClassId,
            taxResult.RatePercent,
            Money.Gross(net, taxResult.RatePercent),
            taxResult.Source,
            taxResult.RatePercent is null ? Money.Gross(net, LegacyFallbackTaxPercent) : null);

        var calls = new[] { itemResponse }.Concat(responses)
            .Select(r => new SourceCall(r.Source, r.Path, r.StatusCode, (long)r.Elapsed.TotalMilliseconds, r.ErrorText))
            .ToList();

        return new ArticleDetail(
            id,
            JsonRead.Str(item["SKU"]) ?? "",
            JsonRead.Str(item["Name"]) ?? "",
            JsonRead.Bool(item["IsActive"]) ?? false,
            JsonRead.Str(item["Identifiers"]?["Gtin"]),
            JsonRead.Str(item["Identifiers"]?["ManufacturerNumber"]),
            JsonRead.Str(item["ShortDescription"]),
            JsonRead.Str(item["Description"]),
            JsonRead.Objects(item["Categories"]).Select(c => new ArticleCategory(JsonRead.Int(c["CategoryId"]) ?? 0, JsonRead.Str(c["Name"]) ?? "")).ToList(),
            NullIfZero(JsonRead.Int(item["ParentItemId"])),
            (item["ChildItems"] as JsonArray)?.Select(JsonRead.Int).OfType<int>().ToList() ?? [],
            price,
            MapSalesChannelPrices(byPart["salesChannelPrices"], taxResult.RatePercent),
            MapSpecialPrice(byPart["specialprices"], taxResult.RatePercent),
            MapVariations(byPart["variations"]),
            MapImages(byPart["images"]),
            new ArticleMeasures(
                JsonRead.Dec(item["Dimensions"]?["Length"]), JsonRead.Dec(item["Dimensions"]?["Width"]), JsonRead.Dec(item["Dimensions"]?["Height"]),
                JsonRead.Dec(item["Weights"]?["ItemWeigth"]), JsonRead.Dec(item["Weights"]?["ShippingWeight"])),
            JsonRead.Date(item["Added"]),
            JsonRead.Date(item["Changed"]),
            issues,
            calls,
            sampleData.Enabled);
    }

    public async ValueTask<JtlResponse> RetrieveRawAsync(int id, string? part, CancellationToken ct = default)
    {
        if (!Parts.TryGetValue(part ?? "item", out var template))
            throw new ArgumentException($"Unbekannter Teil '{part}'. Erlaubt: {string.Join(", ", Parts.Keys)}");
        return await jtl.GetAsync(template.Replace("{id}", id.ToString()), ct);
    }

    // ---------------------------------------------------------------------------------------------

    private static ArticleListItem MapListItem(JsonObject item)
    {
        var issues = ContractValidator.Validate(LegacyContracts.Items, item);
        if (!JsonRead.Objects(item["Categories"]).Any())
            issues.Add(new("items", "Categories", IssueSeverity.Warning, "Keiner Kategorie zugeordnet",
                "Das Alt-Backend lädt Artikel nur über Kategorien – dieser Artikel wird nie in den Shop übernommen."));

        return new ArticleListItem(
            JsonRead.Int(item["Id"]) ?? 0,
            JsonRead.Str(item["SKU"]) ?? "",
            JsonRead.Str(item["Name"]) ?? "",
            JsonRead.Bool(item["IsActive"]) ?? false,
            JsonRead.Dec(item["ItemPriceData"]?["SalesPriceNet"]),
            JsonRead.Int(item["TaxClassId"]),
            JsonRead.Objects(item["Categories"]).Count(),
            NullIfZero(JsonRead.Int(item["ParentItemId"])),
            (item["ChildItems"] as JsonArray)?.Count ?? 0,
            JsonRead.Date(item["Changed"]),
            issues);
    }

    private void AddPartIssues(List<DataIssue> issues, ContractDefinition contract, JtlResponse response)
    {
        if (!response.IsSuccess)
        {
            // 404 bei Unterressourcen heißt meist "keine Daten" – trotzdem sichtbar machen
            var severity = response.StatusCode == 404 ? IssueSeverity.Info : IssueSeverity.Error;
            issues.Add(new(contract.Key, contract.Title, severity,
                response.StatusCode == 404 ? "Keine Daten (404)" : $"{response.Path} → {response.StatusCode} {response.ErrorText}",
                response.StatusCode == 404 ? "–" : contract.LegacyEffect));
            return;
        }

        var records = ContractValidator.Records(response.Body, contract.Shape);
        for (var i = 0; i < records.Count; i++)
            issues.AddRange(ContractValidator.Validate(contract, records[i], contract.Shape == ContractShape.Object ? contract.Title : $"{contract.Title} #{i + 1}"));

        logger.LogDebug("{Contract}: {Count} Datensätze geprüft", contract.Key, records.Count);
    }

    private static List<ArticleSalesChannelPrice> MapSalesChannelPrices(JtlResponse r, decimal? rate) =>
        !r.IsSuccess ? [] : JsonRead.Objects(r.Body).Select(p =>
        {
            var net = JsonRead.Dec(p["NetPrice"]) ?? 0;
            return new ArticleSalesChannelPrice(
                JsonRead.Str(p["SalesChannelId"]) ?? "",
                JsonRead.Int(p["CustomerGroupId"]) ?? 0,
                JsonRead.Dec(p["FromQuantity"]) ?? 0,
                net,
                Money.Gross(net, rate),
                JsonRead.Dec(p["ReduceStandardPriceByPercent"]) ?? 0);
        }).ToList();

    private static ArticleSpecialPrice? MapSpecialPrice(JtlResponse r, decimal? rate)
    {
        if (!r.IsSuccess || r.Body is not JsonObject sp) return null;
        return new ArticleSpecialPrice(
            JsonRead.Bool(sp["IsActive"]) ?? false,
            JsonRead.Date(sp["StartDate"]),
            JsonRead.Date(sp["EndDate"]),
            JsonRead.Bool(sp["EndDateActive"]) ?? false,
            JsonRead.Objects(sp["SpecialPrice"]).Select(e =>
            {
                var net = JsonRead.Dec(e["NetPrice"]) ?? 0;
                return new ArticleSpecialPriceEntry(JsonRead.Bool(e["IsActive"]) ?? false, JsonRead.Int(e["CustomerGroupId"]) ?? 0,
                    JsonRead.Str(e["SalesChannelId"]) ?? "", net, Money.Gross(net, rate));
            }).ToList());
    }

    private static List<ArticleVariation> MapVariations(JtlResponse r) =>
        !r.IsSuccess ? [] : JsonRead.Objects(r.Body).Select(v => new ArticleVariation(
            JsonRead.Int(v["Id"]) ?? 0,
            JsonRead.Str(v["Name"]) ?? JsonRead.Objects(v["Translations"]).Select(t => JsonRead.Str(t["Name"])).FirstOrDefault(n => n is not null) ?? "",
            JsonRead.Int(v["Type"]) ?? 0,
            [])).ToList();

    private static List<ArticleImage> MapImages(JtlResponse r) =>
        !r.IsSuccess ? [] : JsonRead.Objects(r.Body).Select(i => new ArticleImage(
            JsonRead.Int(i["ImageId"]) ?? 0,
            JsonRead.Str(i["SalesChannelId"]) ?? "",
            JsonRead.Int(i["SortNumber"]) ?? 0,
            JsonRead.Int(i["Width"]) ?? 0,
            JsonRead.Int(i["Height"]) ?? 0,
            (long)(JsonRead.Dec(i["Size"]) ?? 0))).ToList();

    private static int? NullIfZero(int? v) => v is null or 0 ? null : v;
}

public sealed class JtlSearchOptions
{
    public const string SectionName = "Jtl:Search";

    /// Name des Suchparameters der JTL-API für GET /v1/items (bei Bedarf anpassen, sobald live geprüft)
    public string Parameter { get; set; } = "searchKeyWord";
}
