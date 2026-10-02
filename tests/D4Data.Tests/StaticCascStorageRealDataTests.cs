using D4Data.Casc;
using D4Data.Casc.Static;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Tests;

/// <summary>
/// Rebuilds part of the Battle.net install in Steam's static layout and reads it back with the Steam reader: CoreTOC, the
/// shared-payload mapping and the first SNOs' meta and payload files must come back byte for byte, through every key layout
/// and encoding. Passes vacuously without a local Battle.net install.
/// </summary>
public class StaticCascStorageRealDataTests
{
    private const int Sampled = 64;

    // false: separators dropped inside folders, a nested manifest the config gives no size for, whole files under
    // rustydemon's hex names behind decoys of another size, and an unreadable nested manifest.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Steam_layout_reads_back_the_Battle_net_files(bool separators)
    {
        if (!SnoStore.TryLocateInstall(out var install) || StaticCascStorage.IsStaticInstall(install)) return;
        using var battleNet = Diablo4Storage.Open(install);
        var casc = battleNet.Casc;
        using var builder = new StaticInstallBuilder { HexWholeFileNames = !separators };
        var files = new List<TvfsWriter.File>();
        var expected = new Dictionary<string, byte[]>();

        void Add(string path, byte[]? content, int layout, Func<byte[], byte[]> encode, string espec)
        {
            if (content is null) return;
            var encoded = encode(content);
            var key = builder.Put(layout, layout, 0, encoded);
            files.Add(new TvfsWriter.File(path, key, encoded.Length, content.Length, espec));
            expected[@"Base\" + path.Replace('/', '\\')] = content;
        }

        Add("CoreTOC.dat", Read(casc, TvfsSnoArchive.CoreTocPath), StaticInstallBuilder.ChildLayout, StaticInstallBuilder.Zlib, "z");
        Add("CoreTOCSharedPayloadsMapping.dat", Read(casc, TvfsSnoArchive.SharedPayloadsPath),
            StaticInstallBuilder.TypedWholeFileLayout, StaticInstallBuilder.BlteRaw, "b:{*=n}");
        foreach (var entry in battleNet.CoreToc.Entries.Take(Sampled))
        {
            Add($"meta/{entry.Id}", Read(casc, Diablo4Storage.SnoPath(entry.Id, SnoFolder.Meta)),
                StaticInstallBuilder.MetaLayout, StaticInstallBuilder.BlteRaw, "n");
            Add($"payload/{entry.Id}", Read(casc, Diablo4Storage.SnoPath(entry.Id, SnoFolder.Payload)),
                StaticInstallBuilder.PayloadLayout, StaticInstallBuilder.BlteZlib, "b:{*=z}");
        }
        var baseKey = builder.PutManifest("vfs-1", TvfsWriter.Write(files, separators), withSizeAndSpec: separators);
        var rootFiles = new List<TvfsWriter.File> { new("Base", baseKey, 0, 0, "z") };
        if (separators) builder.AddConfigLine("build-name = 3.2.2.73764-steam");
        else
        {
            var missing = StaticInstallBuilder.MissingKey();
            builder.AddConfigLine($"vfs-2 = {new string('0', 32)} {Convert.ToHexString(missing)}");
            builder.AddConfigLine("vfs-root-unknown = not-hex");
            rootFiles.Add(new("enUS_Text", missing, 100, 0, "z"));
        }
        builder.PutManifest("vfs-root", TvfsWriter.Write(rootFiles));
        builder.Write();

        Assert.True(StaticCascStorage.IsStaticInstall(builder.Root));
        Assert.True(SnoStore.IsInstall(builder.Root));
        var log = new List<string>();
        using (var steam = StaticCascStorage.Open(builder.Root, log.Add))
        {
            Assert.Equal(separators ? "3.2.2.73764-steam" : "9.9.9.12345", steam.BuildVersion);
            Assert.Equal(!separators, steam.Files.SeparatorsIgnored);
            Assert.True(expected.Count > Sampled / 2, $"only {expected.Count} files to compare");
            foreach (var (path, content) in expected)
                Assert.True(content.AsSpan().SequenceEqual(steam.ReadPath(path)), $"{path} differs; log: {string.Join(" | ", log)}");
            Assert.Null(steam.ReadPath(@"Base\Meta\999999999"));
        }
        Assert.Contains(log, line => line.Contains($"files, build {(separators ? "3.2.2.73764-steam" : "9.9.9.12345")}"));
        if (!separators)
        {
            Assert.Contains(log, line => line.Contains("skipped a nested manifest, vfs-2"));
            Assert.Contains(log, line => line.Contains("layout 2 file found as") && line.Contains("0x"));
        }

        using var store = new SnoStore(builder.Root);
        var first = battleNet.CoreToc.Entries[0];
        Assert.Equal(first.Name, store.Name(first.Id));
        Assert.Equal(battleNet.CoreToc.Entries.Count, store.AllEntries((int)first.Group).Count() + battleNet.CoreToc.Entries.Count(e => e.Group != first.Group));
    }

    private static byte[]? Read(CascStorage casc, string path)
    {
        try { return casc.TryResolvePath(path, out var key) ? casc.Read(key) : null; }
        catch (CascException e) when (e is CascContentNotFoundException or CascEncryptedContentException) { return null; }
    }
}
