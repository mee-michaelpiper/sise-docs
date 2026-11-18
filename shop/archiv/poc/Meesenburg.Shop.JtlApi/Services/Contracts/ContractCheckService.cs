using System.Text.Json.Nodes;
using Meesenburg.Shop.JtlApi.Brokers.Jtl;
using Meesenburg.Shop.JtlApi.Models;
using Meesenburg.Shop.JtlApi.Options;
using Microsoft.Extensions.Options;

namespace Meesenburg.Shop.JtlApi.Services.Contracts;

public interface IContractCheckService
{
    ValueTask<ContractReport> CheckAsync(IReadOnlyList<int> itemIds, string? snapshotName, CancellationToken ct = default);
    ValueTask<SnapshotInfo> CreateSnapshotAsync(IReadOnlyList<int> itemIds, CancellationToken ct = default);
    ValueTask<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(CancellationToken ct = default);
}

/// Prüft die JTL-Antworten gegen die Erwartungen des Alt-Backends und gegen einen gespeicherten Stand.
/// Damit lässt sich belegen, ob JTL tatsächlich etwas geändert hat – oder ob das Alt-Backend Daten verwirft.
public sealed class ContractCheckService(
    IJtlWawiBroker jtl,
    ISnapshotStore snapshots,
    IOptions<JtlOptions> jtlOptions,
    SampleDataInfo sampleData) : IContractCheckService
{
    private const int ListPageSize = 50;

    public async ValueTask<ContractReport> CheckAsync(IReadOnlyList<int> itemIds, string? snapshotName, CancellationToken ct = default)
    {
        var snapshot = snapshotName is null
            ? await snapshots.LoadLatestAsync(ct)
            : await snapshots.LoadAsync(snapshotName, ct);

        var calls = await FetchAllAsync(itemIds, ct);
        var results = calls.Select(c => Evaluate(c, snapshot)).ToList();

        return new ContractReport(DateTimeOffset.UtcNow, itemIds, snapshot?.Name, results, sampleData.Enabled);
    }

    public async ValueTask<SnapshotInfo> CreateSnapshotAsync(IReadOnlyList<int> itemIds, CancellationToken ct = default)
    {
        var calls = await FetchAllAsync(itemIds, ct);
        var snapshot = new Snapshot
        {
            Name = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"),
            CreatedUtc = DateTimeOffset.UtcNow,
            ItemIds = itemIds.ToList(),
            Signatures = calls
                .Where(c => c.Response.IsSuccess)
                .ToDictionary(c => c.Key, c => SchemaSignature.Build(c.Records))
        };
        await snapshots.SaveAsync(snapshot, ct);
        return new SnapshotInfo(snapshot.Name, snapshot.CreatedUtc, snapshot.Signatures.Count, snapshot.ItemIds);
    }

    public ValueTask<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(CancellationToken ct = default) => snapshots.ListAsync(ct);

    // ---------------------------------------------------------------------------------------------

    private sealed record Call(
        string Key,
        ContractDefinition Contract,
        string Path,
        JtlResponse Response,
        IReadOnlyList<JsonObject> Records,
        JtlResponse? LegacyResponse,
        IReadOnlyList<JsonObject> LegacyRecords,
        string? Label);

    private async Task<List<Call>> FetchAllAsync(IReadOnlyList<int> itemIds, CancellationToken ct)
    {
        var calls = new List<Call>();

        // Globale Abfragen
        calls.Add(await FetchAsync(LegacyContracts.Items, LegacyContracts.Items.Path.Replace("{pageSize}", ListPageSize.ToString()), null, null, ct));
        calls.Add(await FetchAsync(LegacyContracts.Categories, LegacyContracts.Categories.Path, null, null, ct));
        var taxClassesCall = await FetchAsync(LegacyContracts.TaxClasses, LegacyContracts.TaxClasses.Path, null, null, ct);
        calls.Add(taxClassesCall);

        // Steuersätze je Steuerklasse (wie das Alt-Backend: Firma fest, DE → DE)
        foreach (var taxClass in taxClassesCall.Records)
        {
            var id = JsonRead.Int(taxClass["Id"]);
            if (id is null) continue;
            var path = LegacyContracts.TaxRate.Path
                .Replace("{taxClassId}", id.Value.ToString())
                .Replace("{companyId}", jtlOptions.Value.CompanyId.ToString());
            var call = await FetchAsync(LegacyContracts.TaxRate, path, null, $"Steuerklasse {id}", ct);
            calls.Add(call with { Key = $"taxRate:{id}" });
        }

        // Je Artikel: Unterressourcen, jeweils mit /v1 und mit dem Pfad des Alt-Backends (ohne /v1)
        foreach (var itemId in itemIds)
        {
            foreach (var contract in LegacyContracts.PerItem)
            {
                var path = contract.Path.Replace("{id}", itemId.ToString());
                var legacy = contract.LegacyPath?.Replace("{id}", itemId.ToString());
                var call = await FetchAsync(contract, path, legacy, $"Artikel {itemId}", ct);
                calls.Add(call with { Key = $"{contract.Key}:{itemId}" });
            }
        }

        return calls;
    }

    private async Task<Call> FetchAsync(ContractDefinition contract, string path, string? legacyPath, string? label, CancellationToken ct)
    {
        var response = await jtl.GetAsync(path, ct);
        var records = ContractValidator.Records(response.Body, contract.Shape);

        JtlResponse? legacyResponse = null;
        IReadOnlyList<JsonObject> legacyRecords = [];
        if (legacyPath is not null && legacyPath != path)
        {
            legacyResponse = await jtl.GetAsync(legacyPath, ct);
            legacyRecords = ContractValidator.Records(legacyResponse.Body, contract.Shape);
        }

        return new Call(contract.Key, contract, path, response, records, legacyResponse, legacyRecords, label);
    }

    private static ContractCheckResult Evaluate(Call call, Snapshot? snapshot)
    {
        var issues = new List<DataIssue>();
        var r = call.Response;

        // 404 bei Unterressourcen eines Artikels = "keine Daten" (z. B. keine Sonderpreise), kein Defekt
        var noData = call.Contract.PerItem && r.StatusCode == 404;
        if (noData)
            issues.Add(new(call.Contract.Key, call.Label ?? call.Contract.Title, IssueSeverity.Info, "Keine Daten (404)", "–"));

        // Das Alt-Backend validiert die Antwort seines eigenen Pfads – sofern abweichend, diese prüfen
        IReadOnlyList<JsonObject> validated =
            call.LegacyResponse is { IsSuccess: true } ? call.LegacyRecords
            : r.IsSuccess ? call.Records
            : [];
        for (var i = 0; i < validated.Count; i++)
        {
            var record = validated[i];
            var recordLabel = call.Contract.Shape == ContractShape.Object ? call.Label
                : $"{call.Label ?? call.Contract.Title} #{RecordId(record) ?? i.ToString()}";
            issues.AddRange(ContractValidator.Validate(call.Contract, record, recordLabel));
        }

        if (r.IsSuccess && r.Body is null)
            issues.Add(new(call.Contract.Key, call.Label ?? "(Antwort)", IssueSeverity.Warning, "Leere Antwort", call.Contract.LegacyEffect));

        if (call.LegacyResponse is { } legacy && legacy.StatusCode != r.StatusCode)
            issues.Add(new(call.Contract.Key, call.Label ?? "(Pfad)", legacy.IsSuccess ? IssueSeverity.Warning : IssueSeverity.Error,
                $"Pfad des Alt-Backends ({legacy.Path}) antwortet mit {legacy.StatusCode}, /v1 mit {r.StatusCode}",
                "Das Alt-Backend prüft den HTTP-Status nicht: eine Fehlerantwort wird als (leere) Daten gelesen."));

        var current = SchemaSignature.Build(call.Records);
        var changes = snapshot is not null && snapshot.Signatures.TryGetValue(call.Key, out var before)
            ? SchemaSignature.Diff(before, current)
            : [];

        var legacyDiff = call.LegacyResponse is { IsSuccess: true } && r.IsSuccess
            ? SchemaSignature.Diff(current, SchemaSignature.Build(call.LegacyRecords))
            : [];

        return new ContractCheckResult(
            call.Key,
            call.Label is null ? call.Contract.Title : $"{call.Contract.Title} · {call.Label}",
            call.Path,
            call.LegacyResponse?.Path,
            r.StatusCode,
            call.LegacyResponse?.StatusCode,
            (long)r.Elapsed.TotalMilliseconds,
            r.IsSuccess || noData ? null : r.ErrorText ?? $"HTTP {r.StatusCode}",
            validated.Count,
            issues,
            changes,
            legacyDiff);
    }

    private static string? RecordId(JsonObject record) =>
        (record["Id"] ?? record["ImageId"] ?? record["SupplierId"] ?? record["CustomFieldId"])?.ToJsonString();
}
