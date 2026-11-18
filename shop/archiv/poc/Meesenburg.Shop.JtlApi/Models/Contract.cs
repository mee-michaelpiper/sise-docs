namespace Meesenburg.Shop.JtlApi.Models;

public enum IssueSeverity { Info, Warning, Error }

/// Abweichung einer JTL-Antwort von dem, was das Alt-Backend (Pydantic-Modelle) erwartet.
public sealed record DataIssue(
    string Contract,
    string Field,
    IssueSeverity Severity,
    string Problem,
    string LegacyEffect);

public sealed record ContractCheckResult(
    string Key,
    string Title,
    string Path,
    string? LegacyPath,
    int StatusCode,
    int? LegacyStatusCode,
    long ElapsedMs,
    string? Error,
    int RecordsChecked,
    IReadOnlyList<DataIssue> Issues,
    IReadOnlyList<SchemaChange> ChangesSinceSnapshot,
    IReadOnlyList<SchemaChange> DifferencesLegacyPath)
{
    public ContractStatus Status =>
        Error is not null || Issues.Any(i => i.Severity == IssueSeverity.Error) ? ContractStatus.Broken
        : ChangesSinceSnapshot.Count > 0 || DifferencesLegacyPath.Count > 0 || Issues.Any(i => i.Severity == IssueSeverity.Warning) ? ContractStatus.Changed
        : ContractStatus.Ok;
}

public enum ContractStatus { Ok, Changed, Broken }

public enum SchemaChangeKind { Added, Removed, TypeChanged }

public sealed record SchemaChange(string Field, SchemaChangeKind Kind, string? Before, string? After);

public sealed record ContractReport(
    DateTimeOffset CheckedUtc,
    IReadOnlyList<int> ItemIds,
    string? ComparedSnapshot,
    IReadOnlyList<ContractCheckResult> Results,
    bool SampleData);

public sealed record SnapshotInfo(string Name, DateTimeOffset CreatedUtc, int Contracts, IReadOnlyList<int> ItemIds);

public sealed record ConnectionStatus(
    DateTimeOffset CheckedUtc,
    bool JtlConfigured,
    bool CustomApiConfigured,
    bool SampleData,
    IReadOnlyList<SourceCall> Calls);
