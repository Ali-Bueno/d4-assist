using System.Text;
using D4Data.Casc;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Tests;

public class SnoTocTests
{
    private const int World = 2, Scene = 5;

    // Two groups, the file's own layout: counts, offsets, an unused array, hashes, one u32, then each group's entries and names.
    private static byte[] File(bool sentinel)
    {
        int[] groups = [0, 0, 2, 0, 0, 1];
        var blocks = new MemoryStream();
        var offsets = new int[groups.Length];
        foreach (var (group, entries) in new[] { (World, new[] { (7, "Sanctuary"), (3, "Limbo") }), (Scene, new[] { (900, "Town_Square") }) })
        {
            offsets[group] = (int)blocks.Position;
            var names = new MemoryStream();
            var writer = new BinaryWriter(blocks);
            foreach (var (id, name) in entries)
            {
                writer.Write(group);
                writer.Write(id);
                writer.Write((int)names.Position);
                names.Write(Encoding.ASCII.GetBytes(name + "\0"));
            }
            writer.Write(names.ToArray());
        }
        var file = new MemoryStream();
        var header = new BinaryWriter(file);
        if (sentinel) header.Write(CoreToc.NewFormatSentinel);
        header.Write(groups.Length);
        foreach (int count in groups) header.Write(count);
        foreach (int offset in offsets) header.Write(offset);
        for (int i = 0; i < groups.Length * 2 + 1; i++) header.Write(0);
        header.Write(blocks.ToArray());
        return file.ToArray();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reads_the_entries_as_WiseOwl_does(bool sentinel)
    {
        var bytes = File(sentinel);
        var expected = CoreToc.Parse(bytes);

        var toc = new SnoToc(bytes);

        Assert.Equal(expected.Entries.Count, toc.Count);
        Assert.Equal(expected.Entries.Select(e => e.Id), toc.Ids);
        foreach (var entry in expected.Entries) Assert.Equal(entry.Name, toc.Name(entry.Id));
        Assert.Equal(expected.EntriesInGroup((SnoGroup)World).Select(e => (e.Id, e.Name)), toc.InGroup(World));
        Assert.Equal(expected.EntriesInGroup((SnoGroup)Scene).Select(e => (e.Id, e.Name)), toc.InGroup(Scene));
    }

    [Fact]
    public void A_sno_or_group_the_file_lacks_reads_as_nothing()
    {
        var toc = new SnoToc(File(sentinel: true));

        Assert.Null(toc.Name(8));
        Assert.Empty(toc.InGroup(1));
    }

    [Fact]
    public void A_file_cut_short_is_reported_as_bad_data()
    {
        Assert.Throws<InvalidDataException>(() => new SnoToc(File(sentinel: true)[..40]));
    }

    [Fact]
    public void Every_entry_of_the_installed_game_reads_as_WiseOwl_reads_it()
    {
        if (!SnoStore.TryLocateInstall(out var install) || !System.IO.File.Exists(Path.Combine(install, ".build.info"))) return;
        using var storage = Diablo4Storage.Open(install);

        var toc = new SnoToc(storage.Casc.ReadPath(TvfsSnoArchive.CoreTocPath));

        Assert.Equal(storage.CoreToc.Entries.Count, toc.Count);
        var first = new Dictionary<int, string>();
        foreach (var entry in storage.CoreToc.Entries) first.TryAdd(entry.Id, entry.Name);
        foreach (var (id, name) in first) Assert.Equal(name, toc.Name(id));
        foreach (var group in storage.CoreToc.Entries.GroupBy(e => e.Group))
            Assert.Equal(group.Select(e => (e.Id, e.Name)), toc.InGroup((int)group.Key));
    }

    [Fact]
    public void The_store_finds_a_name_in_any_case()
    {
        if (!SnoStore.TryLocateInstall(out var install)) return;
        using var store = new SnoStore(install);
        var (id, name) = store.AllEntries((int)SnoGroup.StringList).First();

        Assert.Equal(id, store.Resolve((int)SnoGroup.StringList, name));
        Assert.Equal(id, store.Resolve((int)SnoGroup.StringList, name.ToUpperInvariant()));
        Assert.Equal(id, store.Resolve((int)SnoGroup.StringList, name.ToLowerInvariant()));
        Assert.Equal(name, store.Name(id));
        Assert.Throws<ArgumentException>(() => store.Resolve((int)SnoGroup.StringList, "No_Such_Table"));
    }
}
