using WiseOwl.Casc.Diablo4;

namespace D4Data.Casc;

/// <summary>
/// Diablo IV's SNO files over any TVFS path reader: the same paths and shared-payload rule as WiseOwl's Diablo4Storage
/// (MIT, Diablo4Storage.TryReadSno), for storages WiseOwl can't open.
/// </summary>
internal sealed class TvfsSnoArchive : ISnoArchive
{
    public const string CoreTocPath = @"Base\CoreTOC.dat";
    public const string SharedPayloadsPath = @"Base\CoreTOCSharedPayloadsMapping.dat";

    private readonly Func<string, byte[]?> _readPath;
    private readonly IDisposable? _owner;
    private readonly Lazy<SharedPayloadMapping> _sharedPayloads;

    /// <param name="readPath">The decoded file at a TVFS path; null when the path isn't in the file system.</param>
    public TvfsSnoArchive(Func<string, byte[]?> readPath, string buildVersion, IDisposable? owner = null)
    {
        _readPath = readPath;
        _owner = owner;
        BuildVersion = buildVersion;
        CoreToc = CoreToc.Parse(readPath(CoreTocPath) ?? throw new InvalidDataException($"{CoreTocPath} is not in the game's file system."));
        _sharedPayloads = new(() => readPath(SharedPayloadsPath) is { } bytes ? SharedPayloadMapping.Parse(bytes) : SharedPayloadMapping.Empty);
    }

    public string BuildVersion { get; }

    public CoreToc CoreToc { get; }

    public SharedPayloadMapping SharedPayloads => _sharedPayloads.Value;

    public bool TryReadSno(int group, int id, SnoFolder folder, out byte[] bytes)
    {
        if (_readPath(Diablo4Storage.SnoPath(id, folder)) is { } direct)
        {
            // A present-but-empty payload means "follow the shared-payload alias".
            bytes = folder is SnoFolder.Payload && direct.Length == 0 && TryReadSharedPayload(id, out var aliased) ? aliased : direct;
            return true;
        }
        if (folder is SnoFolder.Payload && TryReadSharedPayload(id, out var shared))
        {
            bytes = shared;
            return true;
        }
        bytes = [];
        return false;
    }

    private bool TryReadSharedPayload(int id, out byte[] bytes)
    {
        bytes = [];
        if (!SharedPayloads.TryGetSource(id, out var source) || _readPath(Diablo4Storage.SnoPath(source, SnoFolder.Payload)) is not { } shared)
            return false;
        bytes = shared;
        return true;
    }

    public void Dispose() => _owner?.Dispose();
}
