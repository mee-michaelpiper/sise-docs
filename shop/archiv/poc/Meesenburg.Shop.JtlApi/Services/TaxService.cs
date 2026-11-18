using Meesenburg.Shop.JtlApi.Brokers.Jtl;
using Meesenburg.Shop.JtlApi.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Meesenburg.Shop.JtlApi.Services;

public sealed record TaxRateResult(decimal? RatePercent, string Source);

public interface ITaxService
{
    ValueTask<TaxRateResult> GetRateAsync(int taxClassId, CancellationToken ct = default);
}

/// Steuersatz je Steuerklasse aus JTL (DE → DE). Kein stiller Rückfallwert:
/// ist der Satz nicht ermittelbar, gibt es keinen Bruttopreis (statt 1 % wie im Alt-Backend).
public sealed class TaxService(IJtlWawiBroker jtl, IMemoryCache cache, IOptions<JtlOptions> options) : ITaxService
{
    public async ValueTask<TaxRateResult> GetRateAsync(int taxClassId, CancellationToken ct = default)
    {
        var key = $"tax:{taxClassId}";
        if (cache.TryGetValue(key, out TaxRateResult? cached) && cached is not null)
            return cached;

        var path = $"v1/tax/taxclass/{taxClassId}/{options.Value.CompanyId}/DE/DE";
        var response = await jtl.GetAsync(path, ct);
        var rate = response.IsSuccess ? JsonRead.Dec(response.Body?["Rate"]) : null;

        var result = rate is null
            ? new TaxRateResult(null, $"nicht ermittelbar ({response.Path} → {(response.StatusCode == 0 ? "keine Verbindung" : response.StatusCode)})")
            : new TaxRateResult(rate, $"JTL {path}");

        // Fehler nur kurz merken, damit ein Ausfall nicht dauerhaft hängen bleibt
        cache.Set(key, result, rate is null ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(15));
        return result;
    }
}

public static class Money
{
    /// Kaufmännisch auf 2 Stellen – überall gleich (eine Stelle der Wahrheit statt Gleitkomma im Frontend).
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal? Gross(decimal? net, decimal? ratePercent) =>
        net is null || ratePercent is null ? null : Round(net.Value * (100m + ratePercent.Value) / 100m);
}
