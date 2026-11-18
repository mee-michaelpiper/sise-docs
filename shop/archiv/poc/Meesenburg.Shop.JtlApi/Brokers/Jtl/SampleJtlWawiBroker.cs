using System.Text.Json.Nodes;
using System.Web;

namespace Meesenburg.Shop.JtlApi.Brokers.Jtl;

public sealed record SampleDataInfo(bool Enabled);

/// Erfundene Beispieldaten im Format der JTL-Wawi-API – NUR zum Testen der Oberfläche ohne JTL-Zugang.
/// Jeder Artikel enthält bewusst eine typische Fehlerkonstellation, damit die Prüfungen sichtbar werden:
/// 1001 sauber · 1002 ReleasedOnDate = null · 1003 ohne Kategorie · 1004 Rabatt 7,5 % (int-Feld)
/// 1005 Steuerklasse ohne Steuersatz (→ 1 % im Alt-Backend) · 1006 inaktiv, Lieferantenbestand 12,5
public sealed class SampleJtlWawiBroker : IJtlWawiBroker
{
    private static readonly JsonArray ItemsData = BuildItems();

    public ValueTask<JtlResponse> GetAsync(string relativePath, CancellationToken ct = default)
    {
        var path = relativePath.TrimStart('/');
        var (status, body) = Route(path);
        return ValueTask.FromResult(new JtlResponse("JTL-Wawi (Beispieldaten)", path, status, TimeSpan.FromMilliseconds(Random.Shared.Next(40, 180)),
            body, status is >= 200 and < 300 ? null : body?.ToJsonString() ?? "Not Found"));
    }

    public async ValueTask<JsonNode> GetRequiredAsync(string relativePath, CancellationToken ct = default)
    {
        var r = await GetAsync(relativePath, ct);
        return r.IsSuccess && r.Body is not null ? r.Body : throw new JtlRequestException(r);
    }

    private static (int, JsonNode?) Route(string path)
    {
        var (p, query) = Split(path);
        var legacy = !p.StartsWith("v1/", StringComparison.Ordinal);
        var segments = (legacy ? p : p[3..]).Split('/', StringSplitOptions.RemoveEmptyEntries);

        switch (segments)
        {
            case ["items"]:
                return (200, Page(query));
            case ["items", var idText] when int.TryParse(idText, out var id):
                return Find(id) is { } item ? (200, item.DeepClone()) : (404, Error("Item not found"));
            case ["items", var idText, var part] when int.TryParse(idText, out var id):
                return Find(id) is null ? (404, Error("Item not found")) : Part(id, part, legacy);
            case ["categories"]:
                return (200, Paged(new JsonArray(
                    Category(10, "Profilzylinder"), Category(11, "Halbzylinder"), Category(12, "Schließanlagen"))));
            case ["taxClasses"]:
                return (200, new JsonArray(
                    new JsonObject { ["Id"] = 1, ["Name"] = "Normaler Steuersatz", ["IsDefault"] = true, ["Type"] = 0 },
                    new JsonObject { ["Id"] = 2, ["Name"] = "Sonderklasse (ohne Satz für Firma)", ["IsDefault"] = false, ["Type"] = 0 }));
            case ["tax", "taxclass", "1", _, "DE", "DE"]:
                return (200, new JsonObject { ["Rate"] = 19.0, ["TaxClassId"] = 1, ["DepartureCountryISO"] = "DE", ["ShipmentCountryISO"] = "DE" });
            case ["tax", "taxclass", "2", ..]:
                return (500, Error("No tax rate configured for company"));
            default:
                return (404, Error("Not Found"));
        }
    }

    private static JsonObject? Find(int id) => ItemsData.OfType<JsonObject>().FirstOrDefault(i => (int?)i["Id"] == id);

    private static JsonObject Page(Dictionary<string, string> query)
    {
        IEnumerable<JsonObject> items = ItemsData.OfType<JsonObject>();
        if (query.TryGetValue("searchKeyWord", out var search) && search.Length > 0)
            items = items.Where(i => ((string?)i["Name"] ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                                     || ((string?)i["SKU"] ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
        var all = items.ToList();
        var size = query.TryGetValue("pageSize", out var s) && int.TryParse(s, out var ps) && ps > 0 ? ps : 100;
        var number = query.TryGetValue("pageNumber", out var n) && int.TryParse(n, out var pn) && pn > 0 ? pn : 1;
        var pageItems = new JsonArray(all.Skip((number - 1) * size).Take(size).Select(i => (JsonNode)i.DeepClone()).ToArray());
        var totalPages = Math.Max(1, (int)Math.Ceiling(all.Count / (double)size));
        return new JsonObject
        {
            ["Items"] = pageItems, ["TotalItems"] = all.Count, ["PageNumber"] = number, ["PageSize"] = size,
            ["TotalPages"] = totalPages, ["HasPreviousPage"] = number > 1, ["HasNextPage"] = number < totalPages,
            ["NextPageNumber"] = Math.Min(number + 1, totalPages), ["PreviousPageNumber"] = Math.Max(1, number - 1),
        };
    }

    private static JsonObject Paged(JsonArray items) => new()
    {
        ["Items"] = items, ["TotalItems"] = items.Count, ["PageNumber"] = 1, ["PageSize"] = 100, ["TotalPages"] = 1,
        ["HasPreviousPage"] = false, ["HasNextPage"] = false, ["NextPageNumber"] = 1, ["PreviousPageNumber"] = 1,
    };

    private static (int, JsonNode?) Part(int id, string part, bool legacy) => part.ToLowerInvariant() switch
    {
        "images" => (200, new JsonArray(Image(id, 501, legacy), Image(id, 502, legacy))),
        "specialprices" => id == 1001
            ? (200, new JsonObject
            {
                ["ItemId"] = id, ["IsActive"] = true, ["StartDate"] = "2026-10-01T00:00:00", ["EndDateActive"] = false,
                ["EndDate"] = null, ["TillAmountActive"] = false, ["TillAmountInStockSmallerThan"] = 0,
                ["SpecialPrice"] = new JsonArray(new JsonObject { ["IsActive"] = true, ["CustomerGroupId"] = 1, ["NetPrice"] = 37.81, ["SalesChannelId"] = "1-1-1" }),
            })
            : (404, Error("No special prices")),
        "saleschannelprices" => (200, new JsonArray(
            ChannelPrice(id, 1, 42.02m, 0),
            ChannelPrice(id, 10, 39.92m, id == 1004 ? 7.5m : 5))),
        "variations" => id is 1001 or 1002
            ? (200, new JsonArray(new JsonObject
            {
                ["Id"] = 9000 + id, ["Name"] = "Länge außen/innen", ["ItemId"] = id, ["Type"] = 0,
                ["Translations"] = new JsonArray(new JsonObject { ["LanguageIso"] = "DE", ["Name"] = "Länge außen/innen" }),
            }))
            : (200, new JsonArray()),
        "suppliers" => (200, new JsonArray(new JsonObject
        {
            ["ItemId"] = id, ["SupplierId"] = 7, ["IsStandard"] = true, ["PurchasePriceNet"] = 21.4, ["TaxRate"] = 19,
            ["MinimumPurchaseQuantity"] = 1, ["PermissibleOrderQuantity"] = 1, ["DeliveryTime"] = 3, ["UseSupplierDeliveryTime"] = true,
            ["Stocklevel"] = id == 1006 ? 12.5 : 40, ["IsDropshipping"] = false, ["MergeStockLevel"] = false,
            ["AmountPackagingUnit"] = 1, ["PackagingUnitPurchasePriceNet"] = 21.4, ["ScalePrices"] = new JsonArray(),
        })),
        "properties" => (200, new JsonObject { ["ItemId"] = id, ["Properties"] = new JsonArray(new JsonObject { ["PropertyId"] = 3, ["PropertyValueId"] = 31 }) }),
        "descriptions" => (200, new JsonArray(new JsonObject
        {
            ["ItemId"] = id, ["Name"] = (string?)Find(id)?["Name"], ["Description"] = "<p>Beispieltext</p>", ["ShortDescription"] = "Beispiel",
            ["SeoPath"] = $"artikel-{id}", ["SeoMetaDescription"] = "", ["SeoTitleTag"] = "", ["SeoMetaKeywords"] = id == 1001 ? "" : null,
            ["LanguageIso"] = "DE", ["SalesChannelId"] = "1-1-1",
        })),
        "customfields" => (200, new JsonArray(new JsonObject { ["CustomFieldId"] = 4, ["Value"] = "SN24" })),
        _ => (404, Error("Not Found"))
    };

    // Alt-Pfad ohne /v1 liefert in den Beispieldaten eine ältere Struktur (zeigt die Versions-Prüfung)
    private static JsonObject Image(int id, int imageId, bool legacy)
    {
        var img = new JsonObject
        {
            ["ItemId"] = id, ["ImageId"] = imageId, ["ImageDataType"] = "jpg", ["SalesChannelId"] = "1-1-1",
            ["SortNumber"] = imageId - 500, ["Size"] = 182_331, ["Width"] = 1200, ["Height"] = 1200,
        };
        if (!legacy) img["AltText"] = "Produktbild";
        return img;
    }

    private static JsonObject ChannelPrice(int id, int fromQty, decimal net, decimal reducePercent) => new()
    {
        ["ItemId"] = id, ["SalesChannelId"] = "1-1-1", ["CustomerGroupId"] = 1, ["FromQuantity"] = fromQty,
        ["NetPrice"] = net, ["ReduceStandardPriceByPercent"] = reducePercent,
    };

    private static JsonObject Category(int id, string name) => new()
    {
        ["Id"] = id, ["Name"] = name, ["Description"] = "", ["ParentCategoryId"] = 0, ["Level"] = 1, ["SortNumber"] = id,
        ["ActiveSalesChannels"] = new JsonArray("1-1-1"),
    };

    private static JsonArray BuildItems() => new(
        Item(1001, "SN-PZ-3030", "Profilzylinder PZ 30/30 mm, 3 Schlüssel", 42.02m, 1, [10]),
        Item(1002, "SN-PZ-3035", "Profilzylinder PZ 30/35 mm, 3 Schlüssel", 44.54m, 1, [10], i => i["ReleasedOnDate"] = null),
        Item(1003, "SN-ZS-01", "Zusatzschlüssel für Profilzylinder", 8.40m, 1, []),
        Item(1004, "SN-HZ-1030", "Halbzylinder HZ 10/30 mm", 31.85m, 1, [11]),
        Item(1005, "SN-SA-BASIS", "Schließanlage Basis (Konfigurator)", 249.00m, 2, [12]),
        Item(1006, "SN-PZ-4045", "Profilzylinder PZ 40/45 mm (ausgelaufen)", 49.58m, 1, [10], i => i["IsActive"] = false));

    private static JsonObject Item(int id, string sku, string name, decimal net, int taxClass, int[] categories, Action<JsonObject>? tweak = null)
    {
        var item = new JsonObject
        {
            ["Id"] = id, ["Name"] = name, ["SKU"] = sku, ["ManufacturerId"] = 3, ["IsActive"] = true,
            ["Categories"] = new JsonArray(categories.Select(c => (JsonNode)new JsonObject { ["CategoryId"] = c, ["Name"] = $"Kategorie {c}" }).ToArray()),
            ["Description"] = "<p>Beispieldaten – kein echter Artikel.</p>", ["ShortDescription"] = "Beispiel",
            ["Identifiers"] = new JsonObject { ["Gtin"] = $"40000000{id}", ["ManufacturerNumber"] = sku, ["Asins"] = new JsonArray() },
            ["Components"] = new JsonArray(), ["ChildItems"] = new JsonArray(), ["ParentItemId"] = 0,
            ["ItemPriceData"] = new JsonObject
            {
                ["SalesPriceNet"] = net, ["SuggestedRetailPrice"] = Math.Round(net * 1.3m, 2), ["PurchasePriceNet"] = Math.Round(net * 0.5m, 2),
                ["EbayPrice"] = 0, ["AmazonPrice"] = 0,
            },
            ["ActiveSalesChannels"] = new JsonArray("1-1-1"), ["SortNumber"] = id,
            ["Added"] = "2026-05-12T09:14:00", ["Changed"] = "2026-10-06T16:02:11", ["ReleasedOnDate"] = "2026-06-01T00:00:00",
            ["StorageOptions"] = new JsonObject
            {
                ["InventoryManagementActive"] = true, ["SplitQuantity"] = false, ["GlobalMinimumStockLevel"] = 0, ["Buffer"] = 0,
                ["SerialNumberItem"] = false, ["SerialNumberTracking"] = false, ["SubjectToShelfLifeExpirationDate"] = false,
                ["SubjectToBatchItem"] = false, ["ProcurementTime"] = 3, ["DetermineProcurementTimeAutomatically"] = false, ["AdditionalHandlingTime"] = 0,
            },
            ["ConditionId"] = 1, ["ShippingClassId"] = 1, ["ProductGroupId"] = 1, ["TaxClassId"] = taxClass,
            ["Dimensions"] = new JsonObject { ["Length"] = 6.0, ["Width"] = 1.7, ["Height"] = 3.3 },
            ["Weights"] = new JsonObject { ["ItemWeigth"] = 0.18, ["ShippingWeight"] = 0.25 },
            ["AllowNegativeStock"] = false,
            ["Quantities"] = new JsonObject { ["MinimumOrderQuantity"] = 1, ["MinimumPurchaseQuantityForCustomerGroup"] = new JsonArray(), ["PermissibleOrderQuantity"] = 1 },
            ["DangerousGoods"] = new JsonObject(), ["SearchTerms"] = "", ["PriceListActive"] = false, ["IgnoreDiscounts"] = false,
        };
        tweak?.Invoke(item);
        return item;
    }

    private static JsonObject Error(string message) => new() { ["Message"] = message };

    private static (string, Dictionary<string, string>) Split(string path)
    {
        var idx = path.IndexOf('?');
        if (idx < 0) return (path, []);
        var q = HttpUtility.ParseQueryString(path[(idx + 1)..]);
        return (path[..idx], q.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => q[k] ?? "", StringComparer.OrdinalIgnoreCase));
    }
}
