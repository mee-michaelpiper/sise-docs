namespace Meesenburg.Shop.JtlApi.Models;

public sealed record ArticlePage(
    int PageNumber,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyList<ArticleListItem> Items,
    bool SampleData);

public sealed record ArticleListItem(
    int Id,
    string Sku,
    string Name,
    bool IsActive,
    decimal? NetPrice,
    int? TaxClassId,
    int CategoryCount,
    int? ParentItemId,
    int ChildCount,
    DateTimeOffset? ChangedUtc,
    IReadOnlyList<DataIssue> Issues)
{
    /// true, wenn das Python-Backend diesen Artikel (bzw. die ganze Seite) beim Import verwerfen würde
    public bool RejectedByLegacy => Issues.Any(i => i.Severity == IssueSeverity.Error);
}

public sealed record ArticleDetail(
    int Id,
    string Sku,
    string Name,
    bool IsActive,
    string? Gtin,
    string? ManufacturerNumber,
    string? ShortDescription,
    string? Description,
    IReadOnlyList<ArticleCategory> Categories,
    int? ParentItemId,
    IReadOnlyList<int> ChildItemIds,
    ArticlePrice Price,
    IReadOnlyList<ArticleSalesChannelPrice> SalesChannelPrices,
    ArticleSpecialPrice? SpecialPrice,
    IReadOnlyList<ArticleVariation> Variations,
    IReadOnlyList<ArticleImage> Images,
    ArticleMeasures Measures,
    DateTimeOffset? AddedUtc,
    DateTimeOffset? ChangedUtc,
    IReadOnlyList<DataIssue> Issues,
    IReadOnlyList<SourceCall> Calls,
    bool SampleData);

public sealed record ArticleCategory(int Id, string Name);

/// Preise immer als decimal, Brutto serverseitig berechnet (eine Stelle der Wahrheit).
public sealed record ArticlePrice(
    decimal? NetPrice,
    decimal? SuggestedRetailPrice,
    decimal? PurchasePriceNet,
    int? TaxClassId,
    decimal? TaxRatePercent,
    decimal? GrossPrice,
    string TaxSource,
    decimal? LegacyFallbackGross);

public sealed record ArticleSalesChannelPrice(
    string SalesChannelId,
    int CustomerGroupId,
    decimal FromQuantity,
    decimal NetPrice,
    decimal? GrossPrice,
    decimal ReduceStandardPriceByPercent);

public sealed record ArticleSpecialPrice(
    bool IsActive,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    bool EndDateActive,
    IReadOnlyList<ArticleSpecialPriceEntry> Entries);

public sealed record ArticleSpecialPriceEntry(bool IsActive, int CustomerGroupId, string SalesChannelId, decimal NetPrice, decimal? GrossPrice);

public sealed record ArticleVariation(int Id, string Name, int Type, IReadOnlyList<string> Values);

public sealed record ArticleImage(int ImageId, string SalesChannelId, int SortNumber, int Width, int Height, long Size);

public sealed record ArticleMeasures(decimal? Length, decimal? Width, decimal? Height, decimal? ItemWeight, decimal? ShippingWeight);

/// Ein Abruf, wie er tatsächlich gemacht wurde – für Nachweise gegenüber JTL bzw. dem Dienstleister.
public sealed record SourceCall(string Source, string Path, int StatusCode, long ElapsedMs, string? Error);
