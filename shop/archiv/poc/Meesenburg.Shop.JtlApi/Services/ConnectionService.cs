using Meesenburg.Shop.JtlApi.Brokers.Jtl;
using Meesenburg.Shop.JtlApi.Models;
using Meesenburg.Shop.JtlApi.Options;
using Microsoft.Extensions.Options;

namespace Meesenburg.Shop.JtlApi.Services;

public interface IConnectionService
{
    ValueTask<ConnectionStatus> CheckAsync(CancellationToken ct = default);
}

public sealed class ConnectionService(
    IJtlWawiBroker jtl,
    ICustomApiBroker customApi,
    IOptions<JtlOptions> jtlOptions,
    IOptions<CustomApiOptions> customOptions,
    SampleDataInfo sampleData) : IConnectionService
{
    public async ValueTask<ConnectionStatus> CheckAsync(CancellationToken ct = default)
    {
        var calls = new List<JtlResponse>
        {
            await jtl.GetAsync("v1/items?pageSize=1&pageNumber=1", ct),
            await jtl.GetAsync("items?pageSize=1&pageNumber=1", ct), // Pfad ohne Version – wie Teile des Alt-Backends
            await jtl.GetAsync("v1/taxClasses", ct),
        };

        if (customOptions.Value.IsConfigured && !sampleData.Enabled)
            calls.Add(await customApi.GetAsync("categories", ct));

        return new ConnectionStatus(
            DateTimeOffset.UtcNow,
            jtlOptions.Value.IsConfigured,
            customOptions.Value.IsConfigured,
            sampleData.Enabled,
            calls.Select(c => new SourceCall(c.Source, c.Path, c.StatusCode, (long)c.Elapsed.TotalMilliseconds, c.ErrorText)).ToList());
    }
}
