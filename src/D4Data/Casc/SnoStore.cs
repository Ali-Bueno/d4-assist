using System.Diagnostics.CodeAnalysis;
using D4Data.Casc.Static;
using D4Data.Format;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Casc;

/// <summary>Read-only SNO access over the local install: CoreTOC name lookups plus Meta/Payload reads.</summary>
public sealed class SnoStore : IDisposable
{
    private readonly ISnoArchive _archive;
    private readonly Dictionary<(int Group, string Name), int> _idsByName = new();
    private readonly Dictionary<int, string> _namesById = new();
    private readonly Dictionary<string, StringListCatalog?> _strings = new();

    /// <param name="log">Where the Steam storage reports each opening step and its first failed reads.</param>
    public SnoStore(string installPath, Action<string>? log = null)
    {
        _archive = OpenArchive(installPath, log);
        foreach (var e in _archive.CoreToc.Entries)
        {
            _idsByName.TryAdd(((int)e.Group, e.Name.ToUpperInvariant()), e.Id);
            _namesById.TryAdd(e.Id, e.Name);
        }
    }

    public string BuildVersion => _archive.BuildVersion;

    /// <summary>Locates the local Diablo IV install: WiseOwl's override and Battle.net entry, then Steam.</summary>
    public static bool TryLocateInstall([NotNullWhen(true)] out string? installPath)
    {
        if (Diablo4Storage.TryLocateInstall(out installPath)) return true;
        installPath = OperatingSystem.IsWindows() ? SteamInstall.Locate(IsInstall) : null;
        return installPath is not null;
    }

    /// <summary>True when <paramref name="folder"/> is an install the store opens: Steam's static storage or a Battle.net CASC.</summary>
    public static bool IsInstall(string folder) =>
        StaticCascStorage.IsStaticInstall(folder) || File.Exists(Path.Combine(folder, BuildInfoFileName));

    private const string BuildInfoFileName = ".build.info";

    // Steam's install also has a .build.info, so its static build config is checked first.
    private static ISnoArchive OpenArchive(string installPath, Action<string>? log)
    {
        try
        {
            if (!StaticCascStorage.IsStaticInstall(installPath)) return new BattleNetSnoArchive(Diablo4Storage.Open(installPath));
            var storage = StaticCascStorage.Open(installPath, log);
            try { return new TvfsSnoArchive(storage.ReadPath, storage.BuildVersion, storage); }
            catch { storage.Dispose(); throw; }
        }
        catch (CascException e) { throw new InvalidDataException(e.Message, e); }
    }

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
        _archive.CoreToc.EntriesInGroup((SnoGroup)group)
            .Where(e => e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(e => (e.Id, e.Name));

    /// <summary>The localized StringList catalog of <paramref name="locale"/> (e.g. enUS, esES), loaded once; null when not installed.</summary>
    public StringListCatalog? Strings(string locale)
    {
        if (!_strings.TryGetValue(locale, out var catalog)) _strings[locale] = catalog = _archive.ReadStrings(locale);
        return catalog;
    }

    /// <summary>Null when the SNO has no meta record or it is encrypted (content not yet released).</summary>
    public SnoBlob? Meta(int group, int id) =>
        TryRead(group, id, SnoFolder.Meta, out var bytes) ? SnoBlob.FromMeta(bytes) : null;

    public SnoBlob? Payload(int group, int id) =>
        TryRead(group, id, SnoFolder.Payload, out var bytes) && bytes.Length > 0
            ? SnoBlob.FromPayload(bytes) : null;

    /// <summary>The text of <paramref name="label"/> in the StringList table named <paramref name="table"/>, in
    /// <paramref name="locale"/> (a D4 code such as deDE); null when the locale, table or label is missing.</summary>
    public string? Text(string locale, string table, string label)
    {
        if (Strings(locale) is not { } strings) return null;
        if (!_idsByName.TryGetValue(((int)SnoGroup.StringList, table.ToUpperInvariant()), out int id)) return null;
        return strings.TryGet(id, label, out string? text) ? text : null;
    }

    // 1087 of 62490 actor metas were TACT-encrypted in build 3.2.1.73552 (census 2026-09-29).
    private bool TryRead(int group, int id, SnoFolder folder, out byte[] bytes)
    {
        try { return _archive.TryReadSno(group, id, folder, out bytes); }
        catch (CascEncryptedContentException) { bytes = []; return false; }
    }

    public void Dispose() => _archive.Dispose();
}
