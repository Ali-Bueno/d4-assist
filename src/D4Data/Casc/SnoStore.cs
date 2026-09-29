using System.Diagnostics.CodeAnalysis;
using D4Data.Format;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Casc;

/// <summary>Read-only SNO access over the local install: CoreTOC name lookups plus Meta/Payload reads.</summary>
public sealed class SnoStore : IDisposable
{
    private readonly Diablo4Storage _storage;
    private readonly Dictionary<(int Group, string Name), int> _idsByName = new();
    private readonly Dictionary<int, string> _namesById = new();

    public SnoStore(string installPath)
    {
        _storage = Diablo4Storage.Open(installPath);
        foreach (var e in _storage.CoreToc.Entries)
        {
            _idsByName.TryAdd(((int)e.Group, e.Name.ToUpperInvariant()), e.Id);
            _namesById.TryAdd(e.Id, e.Name);
        }
    }

    public string BuildVersion => _storage.Casc.Build.Version;

    /// <summary>Locates the local Diablo IV install (wraps WiseOwl so callers never reference it directly).</summary>
    public static bool TryLocateInstall([NotNullWhen(true)] out string? installPath) => Diablo4Storage.TryLocateInstall(out installPath);

    /// <summary>All CoreTOC entries in <paramref name="group"/> (e.g. every Scene SNO for candidate enumeration).</summary>
    public IEnumerable<(int Id, string Name)> AllEntries(int group) => EntriesWithPrefix(group, "");

    /// <summary>Resolves a decimal SNO id or a CoreTOC name within <paramref name="group"/>.</summary>
    public int Resolve(int group, string idOrName)
    {
        if (int.TryParse(idOrName, out var id)) return id;
        return _idsByName.TryGetValue((group, idOrName.ToUpperInvariant()), out id) ? id
            : throw new ArgumentException($"No SNO named '{idOrName}' in group {group}.");
    }

    /// <summary>CoreTOC name of a SNO id (ids are unique across groups).</summary>
    public string? Name(int id) => _namesById.GetValueOrDefault(id);

    public IEnumerable<(int Id, string Name)> EntriesWithPrefix(int group, string prefix) =>
        _storage.CoreToc.EntriesInGroup((SnoGroup)group)
            .Where(e => e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(e => (e.Id, e.Name));

    /// <summary>Null when the SNO has no meta record or it is encrypted (content not yet released).</summary>
    public SnoBlob? Meta(int group, int id) =>
        TryRead(group, id, SnoFolder.Meta, out var bytes) ? SnoBlob.FromMeta(bytes) : null;

    public SnoBlob? Payload(int group, int id) =>
        TryRead(group, id, SnoFolder.Payload, out var bytes) && bytes.Length > 0
            ? SnoBlob.FromPayload(bytes) : null;

    // 1087 of 62490 actor metas were TACT-encrypted in build 3.2.1.73552 (census 2026-09-29).
    private bool TryRead(int group, int id, SnoFolder folder, out byte[] bytes)
    {
        try { return _storage.TryReadSno(group, id, folder, out bytes); }
        catch (CascEncryptedContentException) { bytes = []; return false; }
    }

    public void Dispose() => _storage.Dispose();
}
