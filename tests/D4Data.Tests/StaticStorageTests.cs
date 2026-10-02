using D4Data.Casc.Static;

namespace D4Data.Tests;

public class StaticStorageTests
{
    private const string Config = """
        # Steam build config
        key-layout-index-bits = 2
        key-layout-0 = 8 16 30 0
        key-layout-3 = 8 16 20 4096
        vfs-root = 00112233445566778899aabbccddeeff d41d8cd98f00b2049c004001c1a2b3c4
        vfs-root-size = 1000 400
        vfs-root-espec = z
        vfs-1 = 00112233445566778899aabbccddeeff d41d8cd98f00b2049c004001c1a2b3c5
        vfs-root-extra = not a manifest
        build-uid = fenris
        build-name = 3.2.2.73764
        """;

    [Fact]
    public void Build_config_gives_the_layouts_and_manifests_root_first()
    {
        var config = StaticBuildConfig.Parse(Config);

        Assert.Equal(2, config.IndexBits);
        Assert.Equal(new StaticKeyLayout(8, 16, 30, 1), config.Layouts[0]);
        Assert.Equal(new StaticKeyLayout(8, 16, 20, 4096), config.Layouts[3]);
        Assert.Equal(["vfs-root", "vfs-1"], config.Manifests.Select(m => m.Name));
        Assert.Equal("3.2.2.73764", config.BuildName);
        Assert.Equal(400, config.Root.EncodedSize);
        Assert.Equal("z", config.Root.ESpec);
        Assert.Equal(0, config.Manifests[1].EncodedSize);
        Assert.Null(config.Manifests[1].ESpec);
        Assert.Equal(Convert.FromHexString("d41d8cd98f00b2049c004001c1a2b3c4"), config.Root.Key);
    }

    [Fact]
    public void A_battle_net_build_config_is_not_static()
    {
        Assert.False(StaticBuildConfig.IsStatic(StaticBuildConfig.ParseEntries("encoding = aa bb\nvfs-root = aa bb\n")));
        Assert.False(StaticBuildConfig.IsStatic(StaticBuildConfig.ParseEntries("key-layout-index-bits = 2\nencoding = aa bb\n")));
        Assert.Throws<InvalidDataException>(() => StaticBuildConfig.Parse("vfs-root = aa bb\n"));
    }

    // The worked examples of research/steam-storage.md §3.
    [Fact]
    public void Location_bits_give_layout_chunk_archive_and_offset()
    {
        var config = StaticBuildConfig.Parse(Config);

        var payload = StaticLocation.Decode(Convert.FromHexString("d41d8cd98f00b2049cc1400a80048c00"), config.IndexBits, config.Layouts);
        Assert.Equal((3, 5L, 0x2AL, 0x123000L), (payload.LayoutIndex, payload.Chunk, payload.Archive, payload.Offset));
        Assert.Equal(Path.Combine("005", "0x002a-payload.dat"), payload.ArchiveNames().First());

        var meta = StaticLocation.Decode(Convert.FromHexString("d41d8cd98f00b2049c004001c1a2b3c4"), config.IndexBits, config.Layouts);
        Assert.Equal((0, 1L, 7L, 0x01A2B3C4L), (meta.LayoutIndex, meta.Chunk, meta.Archive, meta.Offset));
        Assert.Equal(Path.Combine("001", "0x0007-meta.dat"), meta.ArchiveNames().First());
    }

    [Fact]
    public void A_layout_missing_from_the_config_is_an_error()
    {
        var config = StaticBuildConfig.Parse(Config);
        // Layout index 1 (bits 55..54 = 01).
        Assert.Throws<InvalidDataException>(() =>
            StaticLocation.Decode(Convert.FromHexString("d41d8cd98f00b2049c40000000000000"), config.IndexBits, config.Layouts));
    }

    [Fact]
    public void Whole_file_layouts_try_TACTLib_names_first_then_rustydemon_guesses()
    {
        var whole = new StaticKeyLayout(8, 32, 0, 1);
        var typed = new StaticLocation(1, whole, 2, (7L << 16) | 3, 0);
        var child = new StaticLocation(2, whole, 2, 26, 0);

        Assert.Equal(Path.Combine("002", "7-0002.dat"), typed.ArchiveNames().First());
        Assert.Equal(Path.Combine("002", "7-payload.dat"), new StaticLocation(1, whole, 2, 7L << 16, 0).ArchiveNames().First());
        Assert.Equal(
            ["26-child.dat", "1a-child.dat", "1a-payload.dat", "0x001a-child.dat", "0x001a-payload.dat"],
            child.ArchiveNames().Select(Path.GetFileName));
    }

    [Fact]
    public void Data_decodes_as_blte_zlib_or_plain_bytes()
    {
        byte[] content = [1, 2, 3, 4, 5, 0xEF, 0xBE, 0xAD, 0xDE];

        Assert.Equal(content, StaticCascStorage.Decode(StaticInstallBuilder.BlteRaw(content), null));
        Assert.Equal(content, StaticCascStorage.Decode(StaticInstallBuilder.BlteZlib(content), "b:{*=z}"));
        Assert.Equal(content, StaticCascStorage.Decode(StaticInstallBuilder.Zlib(content), "z"));
        Assert.Equal(content, StaticCascStorage.Decode(StaticInstallBuilder.Zlib(content), null));
        Assert.Equal(content, StaticCascStorage.Decode(content, null));
    }
}
