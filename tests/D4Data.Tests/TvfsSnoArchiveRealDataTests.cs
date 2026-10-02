using D4Data.Casc;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Tests;

/// <summary>
/// The SNO layer the Steam storage uses, run over the Battle.net install's own paths: it must read exactly what WiseOwl reads.
/// Passes vacuously without a local Battle.net install.
/// </summary>
public class TvfsSnoArchiveRealDataTests
{
    private const int SampledPerGroup = 40;

    [Fact]
    public void Reads_the_same_toc_and_files_as_WiseOwl()
    {
        if (!SnoStore.TryLocateInstall(out var install) || !File.Exists(Path.Combine(install, ".build.info"))) return;
        using var storage = Diablo4Storage.Open(install);
        var casc = storage.Casc;
        using var archive = new TvfsSnoArchive(path => casc.TryResolvePath(path, out var key) ? casc.Read(key) : null, casc.Build.Version);

        Assert.Equal(storage.CoreToc.Entries.Count, archive.CoreToc.Entries.Count);
        Assert.Equal(storage.SharedPayloads.Count, archive.SharedPayloads.Count);
        int compared = 0;
        foreach (var group in storage.CoreToc.Entries.GroupBy(e => e.Group))
            foreach (var entry in group.Take(SampledPerGroup))
                foreach (var folder in new[] { SnoFolder.Meta, SnoFolder.Payload })
                {
                    bool expectedFound, found;
                    byte[] expected, actual;
                    try { expectedFound = storage.TryReadSno(entry.Group, entry.Id, folder, out expected); }
                    catch (CascEncryptedContentException) { continue; }
                    found = archive.TryReadSno((int)entry.Group, entry.Id, folder, out actual);
                    Assert.Equal(expectedFound, found);
                    Assert.Equal(expected, actual);
                    compared++;
                }
        Assert.True(compared > 1000, $"only {compared} files compared");
    }

    [Fact]
    public void Shared_payloads_resolve_like_WiseOwl()
    {
        if (!SnoStore.TryLocateInstall(out var install) || !File.Exists(Path.Combine(install, ".build.info"))) return;
        using var storage = Diablo4Storage.Open(install);
        var casc = storage.Casc;
        using var archive = new TvfsSnoArchive(path => casc.TryResolvePath(path, out var key) ? casc.Read(key) : null, casc.Build.Version);

        int compared = 0;
        foreach (var (target, _) in storage.SharedPayloads.Pairs().Take(SampledPerGroup * 10))
        {
            bool expectedFound;
            byte[] expected;
            // Optional content (e.g. high-resolution textures) may not be downloaded.
            try { expectedFound = storage.TryReadSno(0, target, SnoFolder.Payload, out expected); }
            catch (CascException e) when (e is CascContentNotFoundException or CascEncryptedContentException) { continue; }
            Assert.Equal(expectedFound, archive.TryReadSno(0, target, SnoFolder.Payload, out var actual));
            Assert.Equal(expected, actual);
            compared++;
        }
        Assert.True(compared > 0, "no shared payload was downloaded");
    }
}
