using System.Text.Json;
using Meesenburg.Shop.JtlApi.Models;
using Meesenburg.Shop.JtlApi.Options;
using Microsoft.Extensions.Options;

namespace Meesenburg.Shop.JtlApi.Services.Contracts;

public sealed class Snapshot
{
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
    public List<int> ItemIds { get; set; } = [];
    public Dictionary<string, SortedDictionary<string, SortedSet<string>>> Signatures { get; set; } = [];
}

public interface ISnapshotStore
{
    ValueTask SaveAsync(Snapshot snapshot, CancellationToken ct = default);
    ValueTask<Snapshot?> LoadLatestAsync(CancellationToken ct = default);
    ValueTask<Snapshot?> LoadAsync(string name, CancellationToken ct = default);
    ValueTask<IReadOnlyList<SnapshotInfo>> ListAsync(CancellationToken ct = default);
}

/// Ablage als JSON-Datei je Stand – nachvollziehbar und als Beleg weitergebbar (keine Werte, nur Struktur).
public sealed class FileSnapshotStore(IOptions<SnapshotOptions> options, IHostEnvironment env) : ISnapshotStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private string Dir
    {
        get
        {
            var dir = Path.IsPathRooted(options.Value.Directory)
                ? options.Value.Directory
                : Path.Combine(env.ContentRootPath, options.Value.Directory);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public async ValueTask SaveAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        await using var stream = File.Create(Path.Combine(Dir, snapshot.Name + ".json"));
        await JsonSerializer.SerializeAsync(stream, snapshot, Json, ct);
    }

    public async ValueTask<Snapshot?> LoadLatestAsync(CancellationToken ct = default)
    {
        var latest = Directory.EnumerateFiles(Dir, "*.json").OrderDescending().FirstOrDefault();
        return latest is null ? null : await ReadAsync(latest, ct);
    }

    public async ValueTask<Snapshot?> LoadAsync(string name, CancellationToken ct = default)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        var file = Path.Combine(Dir, name + ".json");
        return File.Exists(file) ? await ReadAsync(file, ct) : null;
    }

    public async ValueTask<IReadOnlyList<SnapshotInfo>> ListAsync(CancellationToken ct = default)
    {
        var result = new List<SnapshotInfo>();
        foreach (var file in Directory.EnumerateFiles(Dir, "*.json").OrderDescending())
        {
            var s = await ReadAsync(file, ct);
            if (s is not null) result.Add(new(s.Name, s.CreatedUtc, s.Signatures.Count, s.ItemIds));
        }
        return result;
    }

    private static async ValueTask<Snapshot?> ReadAsync(string file, CancellationToken ct)
    {
        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<Snapshot>(stream, Json, ct);
    }
}
