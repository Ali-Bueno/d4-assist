using WiseOwl.Casc.Diablo4;

namespace D4Data.Casc;

/// <summary>The SNO files of one Diablo IV install, whichever storage holds them (Battle.net CASC or Steam's static one).</summary>
internal interface ISnoArchive : IDisposable
{
    string BuildVersion { get; }

    CoreToc CoreToc { get; }

    /// <summary>False when the SNO has no file in <paramref name="folder"/>; throws CascEncryptedContentException when it is encrypted.</summary>
    bool TryReadSno(int group, int id, SnoFolder folder, out byte[] bytes);
}

/// <summary>The Battle.net install, read by WiseOwl.</summary>
internal sealed class BattleNetSnoArchive(Diablo4Storage storage) : ISnoArchive
{
    public string BuildVersion => storage.Casc.Build.Version;

    public CoreToc CoreToc => storage.CoreToc;

    public bool TryReadSno(int group, int id, SnoFolder folder, out byte[] bytes) => storage.TryReadSno(group, id, folder, out bytes);

    public void Dispose() => storage.Dispose();
}
