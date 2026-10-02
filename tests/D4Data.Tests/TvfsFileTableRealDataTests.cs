using D4Data.Casc;
using D4Data.Casc.Static;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Tests;

/// <summary>Walks the Battle.net install's TVFS with the Steam reader's parser: every path must lead to WiseOwl's key. Passes vacuously without the install.</summary>
public class TvfsFileTableRealDataTests
{
    private const int HashPrefixLength = 9;
    private const int ArchiveEnvelopeSize = 30; // a Battle.net data.NNN entry's header before its BLTE blob (WiseOwl CascStorage)

    [Fact]
    public void Every_path_WiseOwl_knows_resolves_to_the_same_key()
    {
        if (!SnoStore.TryLocateInstall(out var install) || !File.Exists(Path.Combine(install, ".build.info"))) return;
        using var storage = Diablo4Storage.Open(install);
        var casc = storage.Casc;
        var manifests = casc.Config.VfsManifestEncodingKeys().Select(k => k.IndexPrefix).ToHashSet();

        var table = TvfsFileTable.Parse(
            casc.Read(casc.Config.VfsRoot!.Value.Encoding),
            span => manifests.Contains(KeyOf(span).IndexPrefix) ? casc.Read(KeyOf(span)) : null);

        Assert.Equal(casc.Tvfs!.Entries.Count, table.Count);
        foreach (var (hash, key) in casc.Tvfs.Entries)
        {
            Assert.True(table.TryResolve(hash, out var spans));
            Assert.Equal(key.IndexPrefix, KeyOf(spans[0]).IndexPrefix);
            Assert.All(spans, s => Assert.True(s.EncodedSize > 0));
        }
        Assert.True(table.TryResolve(TvfsSnoArchive.CoreTocPath, out _));
    }

    [Fact]
    public void Encoded_sizes_match_the_local_index()
    {
        if (!SnoStore.TryLocateInstall(out var install) || !File.Exists(Path.Combine(install, ".build.info"))) return;
        using var storage = Diablo4Storage.Open(install);
        var casc = storage.Casc;
        var manifests = casc.Config.VfsManifestEncodingKeys().Select(k => k.IndexPrefix).ToHashSet();
        var table = TvfsFileTable.Parse(
            casc.Read(casc.Config.VfsRoot!.Value.Encoding),
            span => manifests.Contains(KeyOf(span).IndexPrefix) ? casc.Read(KeyOf(span)) : null);

        int compared = 0;
        foreach (var (hash, _) in casc.Tvfs!.Entries)
        {
            table.TryResolve(hash, out var spans);
            if (!casc.Index.TryGetLocation(KeyOf(spans[0]), out var location)) continue; // not downloaded
            Assert.Equal(location.Size - ArchiveEnvelopeSize, spans[0].EncodedSize);
            compared++;
        }
        Assert.True(compared > 1000, $"only {compared} sizes compared");
    }

    private static EncodingKey KeyOf(TvfsSpan span) => EncodingKey.FromBytes(span.Key().AsSpan(0, HashPrefixLength));
}
