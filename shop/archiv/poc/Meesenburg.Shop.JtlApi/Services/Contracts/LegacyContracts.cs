namespace Meesenburg.Shop.JtlApi.Services.Contracts;

public enum JsonKind { Int, Number, String, Bool, Array, Object, DateTime }

/// Feldregel aus den Pydantic-Modellen des Alt-Backends.
/// Required = Feld ohne Default (fehlt es, scheitert die Validierung).
/// Nullable = Typ ist Optional (sonst scheitert auch ein explizites null – Pydantic-Defaults greifen nur bei fehlendem Feld).
/// Pfad-Syntax: "A.B" für Unterobjekte, "A[].B" für jedes Element eines Arrays.
public sealed record FieldRule(string Path, JsonKind Kind, bool Required = true, bool Nullable = false);

public enum ContractShape { PagedList, List, Object }

public sealed record ContractDefinition(
    string Key,
    string Title,
    string Path,
    string? LegacyPath,
    ContractShape Shape,
    bool PerItem,
    string LegacyEffect,
    FieldRule[] Rules);

/// Erwartungen des FastAPI-Backends an die JTL-Wawi-API, 1:1 aus
/// backend/items_svc/src/jtl/schemas/item_raw_schemas.py übernommen (Stand main, 05.10.2026).
/// {id} = Artikel-Id. LegacyPath = Pfad, den das Alt-Backend tatsächlich aufruft (teils ohne /v1).
public static class LegacyContracts
{
    private static FieldRule R(string p, JsonKind k) => new(p, k);
    private static FieldRule Opt(string p, JsonKind k) => new(p, k, Required: false);

    public static readonly ContractDefinition Items = new(
        "items", "Artikel (Liste)", "v1/items?pageSize={pageSize}&pageNumber=1", null, ContractShape.PagedList, false,
        "RawAllItems(items=…) validiert die ganze Seite (100 Artikel). Ein Fehler in einem Artikel bricht den Abgleich ab; " +
        "der Worker verschluckt den Fehler (suppress(Exception)) – alle folgenden Artikel bleiben veraltet.",
        [
            R("Id", JsonKind.Int), Opt("Name", JsonKind.String), R("SKU", JsonKind.String), Opt("ManufacturerId", JsonKind.Int),
            R("IsActive", JsonKind.Bool), R("Categories", JsonKind.Array), R("Categories[].CategoryId", JsonKind.Int),
            Opt("Categories[].Name", JsonKind.String),
            R("Description", JsonKind.String), R("ShortDescription", JsonKind.String),
            R("Identifiers", JsonKind.Object), R("Identifiers.Gtin", JsonKind.String),
            R("Identifiers.ManufacturerNumber", JsonKind.String), R("Identifiers.Asins", JsonKind.Array),
            R("Components", JsonKind.Array), R("ChildItems", JsonKind.Array), R("ParentItemId", JsonKind.Int),
            R("ItemPriceData", JsonKind.Object), R("ItemPriceData.SalesPriceNet", JsonKind.Number),
            R("ItemPriceData.SuggestedRetailPrice", JsonKind.Number), R("ItemPriceData.PurchasePriceNet", JsonKind.Number),
            R("ItemPriceData.EbayPrice", JsonKind.Number), R("ItemPriceData.AmazonPrice", JsonKind.Number),
            R("ActiveSalesChannels", JsonKind.Array), R("SortNumber", JsonKind.Int),
            R("Added", JsonKind.DateTime), R("Changed", JsonKind.DateTime), R("ReleasedOnDate", JsonKind.DateTime),
            R("StorageOptions", JsonKind.Object),
            R("StorageOptions.InventoryManagementActive", JsonKind.Bool), R("StorageOptions.SplitQuantity", JsonKind.Bool),
            R("StorageOptions.GlobalMinimumStockLevel", JsonKind.Number), R("StorageOptions.Buffer", JsonKind.Int),
            R("StorageOptions.SerialNumberItem", JsonKind.Bool), R("StorageOptions.SerialNumberTracking", JsonKind.Bool),
            R("StorageOptions.SubjectToShelfLifeExpirationDate", JsonKind.Bool), R("StorageOptions.SubjectToBatchItem", JsonKind.Bool),
            R("StorageOptions.ProcurementTime", JsonKind.Int), R("StorageOptions.DetermineProcurementTimeAutomatically", JsonKind.Bool),
            R("StorageOptions.AdditionalHandlingTime", JsonKind.Int),
            R("ConditionId", JsonKind.Int), Opt("ShippingClassId", JsonKind.Int), R("ProductGroupId", JsonKind.Int),
            R("TaxClassId", JsonKind.Int),
            R("Dimensions", JsonKind.Object), R("Dimensions.Length", JsonKind.Number), R("Dimensions.Width", JsonKind.Number),
            R("Dimensions.Height", JsonKind.Number),
            R("Weights", JsonKind.Object), R("Weights.ItemWeigth", JsonKind.Number), R("Weights.ShippingWeight", JsonKind.Number),
            R("AllowNegativeStock", JsonKind.Bool),
            R("Quantities", JsonKind.Object), R("Quantities.MinimumOrderQuantity", JsonKind.Number),
            R("Quantities.MinimumPurchaseQuantityForCustomerGroup", JsonKind.Array),
            R("Quantities.MinimumPurchaseQuantityForCustomerGroup[].CustomerGroupId", JsonKind.Int),
            R("Quantities.MinimumPurchaseQuantityForCustomerGroup[].PermissibleOrderQuantity", JsonKind.Number),
            R("Quantities.MinimumPurchaseQuantityForCustomerGroup[].MinimumPurchaseQuantity", JsonKind.Number),
            R("Quantities.MinimumPurchaseQuantityForCustomerGroup[].IsActive", JsonKind.Bool),
            R("Quantities.PermissibleOrderQuantity", JsonKind.Number),
            R("DangerousGoods", JsonKind.Object), R("SearchTerms", JsonKind.String),
            R("PriceListActive", JsonKind.Bool), R("IgnoreDiscounts", JsonKind.Bool),
        ]);

    public static readonly ContractDefinition Categories = new(
        "categories", "Kategorien", "v1/categories?pageSize=100&pageNumber=1", null, ContractShape.PagedList, false,
        "get_all_categories() hat keine Fehlerbehandlung: ein Fehler stoppt den kompletten Artikelabgleich " +
        "(Artikel werden nur über Kategorien geladen).",
        [
            R("Id", JsonKind.Int), Opt("Name", JsonKind.String), R("Description", JsonKind.String),
            Opt("ParentCategoryId", JsonKind.Int), Opt("Level", JsonKind.Int), R("SortNumber", JsonKind.Int),
            R("ActiveSalesChannels", JsonKind.Array),
        ]);

    public static readonly ContractDefinition TaxClasses = new(
        "taxClasses", "Steuerklassen", "v1/taxClasses", null, ContractShape.List, false,
        "fetch_as_pydantic_list liefert bei einem Fehler [] – dann gibt es keine Steuerklassen und alle Bruttopreise fallen auf den Rückfallwert.",
        [R("Id", JsonKind.Int), Opt("Name", JsonKind.String), R("IsDefault", JsonKind.Bool), R("Type", JsonKind.Int)]);

    public static readonly ContractDefinition TaxRate = new(
        "taxRate", "Steuersatz je Steuerklasse", "v1/tax/taxclass/{taxClassId}/{companyId}/DE/DE", null, ContractShape.Object, false,
        "fetch_as_pydantic liefert bei einem Fehler None → prepare__tax_classes setzt den Steuersatz auf 1 % (statt z. B. 19 %). " +
        "Bruttopreise wären dann ca. 15 % zu niedrig.",
        [R("Rate", JsonKind.Number), R("TaxClassId", JsonKind.Int), R("DepartureCountryISO", JsonKind.String), R("ShipmentCountryISO", JsonKind.String)]);

    public static readonly ContractDefinition Images = new(
        "images", "Bilder", "v1/items/{id}/images", "items/{id}/images", ContractShape.List, true,
        "Ein fehlerhafter Eintrag → alle Bilder des Artikels fallen weg (fetch_as_pydantic_list → []).",
        [
            R("ItemId", JsonKind.Int), R("ImageId", JsonKind.Int), R("ImageDataType", JsonKind.String),
            R("SalesChannelId", JsonKind.String), R("SortNumber", JsonKind.Int), R("Size", JsonKind.Int),
            R("Width", JsonKind.Int), R("Height", JsonKind.Int),
        ]);

    public static readonly ContractDefinition SpecialPrices = new(
        "specialPrices", "Sonderpreise", "v1/items/{id}/specialprices", "items/{id}/specialprices", ContractShape.Object, true,
        "Fehler → None → get_special_prices liefert [] und verwirft dabei auch die Verkaufskanal-Preise.",
        [
            R("ItemId", JsonKind.Int), R("SpecialPrice", JsonKind.Array), R("SpecialPrice[].IsActive", JsonKind.Bool),
            R("SpecialPrice[].CustomerGroupId", JsonKind.Int), R("SpecialPrice[].NetPrice", JsonKind.Number),
            R("SpecialPrice[].SalesChannelId", JsonKind.String), R("IsActive", JsonKind.Bool),
            R("StartDate", JsonKind.DateTime), R("EndDateActive", JsonKind.Bool), R("EndDate", JsonKind.DateTime),
            R("TillAmountActive", JsonKind.Bool), R("TillAmountInStockSmallerThan", JsonKind.Int),
        ]);

    public static readonly ContractDefinition SalesChannelPrices = new(
        "salesChannelPrices", "Verkaufskanal-Preise", "v1/items/{id}/salesChannelPrices", "items/{id}/salesChannelPrices", ContractShape.List, true,
        "Ein fehlerhafter Eintrag (z. B. Staffel ab 2,5 Stk. oder 7,5 % Rabatt bei int-Feldern) → alle Kanalpreise des Artikels fallen weg.",
        [
            R("ItemId", JsonKind.Int), R("SalesChannelId", JsonKind.String), R("CustomerGroupId", JsonKind.Int),
            R("FromQuantity", JsonKind.Int), R("NetPrice", JsonKind.Number), R("ReduceStandardPriceByPercent", JsonKind.Int),
        ]);

    public static readonly ContractDefinition Variations = new(
        "variations", "Variationen", "v1/items/{id}/variations", "items/{id}/variations", ContractShape.List, true,
        "Fehler → keine Variationen; Konfigurator zeigt Optionen (z. B. Zylinderlängen) nicht oder falsch.",
        [
            R("Id", JsonKind.Int), Opt("Name", JsonKind.String), R("ItemId", JsonKind.Int), R("Type", JsonKind.Int),
            R("Translations", JsonKind.Array), R("Translations[].LanguageIso", JsonKind.String), Opt("Translations[].Name", JsonKind.String),
        ]);

    public static readonly ContractDefinition Properties = new(
        "properties", "Merkmale", "v1/items/{id}/properties", "items/{id}/properties", ContractShape.Object, true,
        "Fehler → None → Artikel ohne Merkmale (Filter und Konfigurator-Zuordnung fehlen).",
        [R("ItemId", JsonKind.Int), R("Properties", JsonKind.Array), R("Properties[].PropertyId", JsonKind.Int), R("Properties[].PropertyValueId", JsonKind.Int)]);

    public static readonly ContractDefinition Descriptions = new(
        "descriptions", "Beschreibungen", "v1/items/{id}/descriptions", "items/{id}/descriptions", ContractShape.List, true,
        "Ein Eintrag mit null (z. B. leere SEO-Felder) → alle Beschreibungen fallen weg.",
        [
            R("ItemId", JsonKind.Int), Opt("Name", JsonKind.String), R("Description", JsonKind.String),
            R("ShortDescription", JsonKind.String), R("SeoPath", JsonKind.String), R("SeoMetaDescription", JsonKind.String),
            R("SeoTitleTag", JsonKind.String), R("SeoMetaKeywords", JsonKind.String), R("LanguageIso", JsonKind.String),
            R("SalesChannelId", JsonKind.String),
        ]);

    public static readonly ContractDefinition CustomFields = new(
        "customFields", "Eigene Felder", "v1/items/{id}/customfields", "items/{id}/customfields", ContractShape.List, true,
        "Fehler → keine eigenen Felder (z. B. Konfigurator-Kennzeichen).",
        [R("CustomFieldId", JsonKind.Int), Opt("Value", JsonKind.String)]);

    public static readonly ContractDefinition Suppliers = new(
        "suppliers", "Lieferanten", "v1/items/{id}/suppliers", "items/{id}/suppliers", ContractShape.List, true,
        "Fehler (z. B. Bestand 12,5 bei int-Feld Stocklevel) → keine Lieferantendaten (Lieferzeit, Bestand).",
        [
            R("ItemId", JsonKind.Int), R("SupplierId", JsonKind.Int), R("IsStandard", JsonKind.Bool),
            R("PurchasePriceNet", JsonKind.Number), R("TaxRate", JsonKind.Int), R("MinimumPurchaseQuantity", JsonKind.Int),
            R("PermissibleOrderQuantity", JsonKind.Int), R("DeliveryTime", JsonKind.Int), R("UseSupplierDeliveryTime", JsonKind.Bool),
            R("Stocklevel", JsonKind.Int), R("IsDropshipping", JsonKind.Bool), R("MergeStockLevel", JsonKind.Bool),
            R("AmountPackagingUnit", JsonKind.Int), R("PackagingUnitPurchasePriceNet", JsonKind.Number), R("ScalePrices", JsonKind.Array),
        ]);

    public static readonly ContractDefinition[] Global = [Items, Categories, TaxClasses];

    public static readonly ContractDefinition[] PerItem =
        [Images, SpecialPrices, SalesChannelPrices, Variations, Properties, Descriptions, CustomFields, Suppliers];
}
