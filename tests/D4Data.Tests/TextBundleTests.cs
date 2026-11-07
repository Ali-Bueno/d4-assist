using System.Text;
using D4Data.Casc;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Tests;

public class TextBundleTests
{
    private const int Waypoints = 501, Empty = 502, Zones = 503;

    // The bundle's layout: magic, count, {SNO, size} pairs, then each table 8-aligned with its 40-byte records and their strings.
    private static byte[] Bundle()
    {
        var tables = new (int Sno, byte[] Bytes)[] { (Waypoints, Table(("Name", "Kyovashad"), ("Desc", "Ciudad\0"), ("", "no label"))), (Empty, new byte[32]), (Zones, Table(("Name", "Fractured Peaks"))) };
        var file = new MemoryStream();
        var writer = new BinaryWriter(file);
        writer.Write(StringListCatalog.Magic);
        writer.Write(tables.Length);
        foreach (var (sno, bytes) in tables)
        {
            writer.Write(sno);
            writer.Write(bytes.Length);
        }
        foreach (var (_, bytes) in tables)
        {
            while (file.Position % 8 != 0) writer.Write((byte)0);
            writer.Write(bytes);
        }
        return file.ToArray();
    }

    private static byte[] Table(params (string Label, string Text)[] entries)
    {
        const int header = 32, record = 40;
        var strings = new MemoryStream();
        var table = new byte[header + entries.Length * record];
        BitConverter.TryWriteBytes(table.AsSpan(20), entries.Length * record);
        int Add(string s)
        {
            int at = table.Length + (int)strings.Position;
            strings.Write(Encoding.UTF8.GetBytes(s));
            return at;
        }
        for (int i = 0; i < entries.Length; i++)
        {
            int at = header + i * record;
            BitConverter.TryWriteBytes(table.AsSpan(at + 8), Add(entries[i].Label));
            BitConverter.TryWriteBytes(table.AsSpan(at + 12), Encoding.UTF8.GetByteCount(entries[i].Label));
            BitConverter.TryWriteBytes(table.AsSpan(at + 24), Add(entries[i].Text));
            BitConverter.TryWriteBytes(table.AsSpan(at + 28), Encoding.UTF8.GetByteCount(entries[i].Text));
        }
        return [.. table, .. strings.ToArray()];
    }

    private static void AssertSameAs(StringListCatalog expected, TextBundle bundle)
    {
        foreach (var (sno, table) in expected.Tables)
            foreach (var (label, text) in table.Entries)
            {
                Assert.True(bundle.TryGet(sno, label, out var found), $"{sno} {label}");
                Assert.Equal(text, found);
            }
    }

    [Fact]
    public void Reads_the_texts_as_WiseOwl_does()
    {
        var bytes = Bundle();
        var expected = StringListCatalog.Parse(bytes, "enUS");

        AssertSameAs(expected, new TextBundle(bytes));
        Assert.Equal(3, expected.TableCount);
        Assert.Equal("Kyovashad", expected.Table(Waypoints)!.Entries["Name"]);
    }

    [Fact]
    public void A_table_or_label_the_bundle_lacks_reads_as_nothing()
    {
        var bundle = new TextBundle(Bundle());

        Assert.False(bundle.TryGet(Waypoints, "Missing", out var text));
        Assert.Equal(string.Empty, text);
        Assert.False(bundle.TryGet(Empty, "Name", out _));
        Assert.False(bundle.TryGet(999, "Name", out _));
    }

    [Fact]
    public void A_file_that_is_no_bundle_is_reported_as_bad_data()
    {
        Assert.Throws<InvalidDataException>(() => new TextBundle(new byte[64]));
    }

    [Fact]
    public void Every_text_of_an_installed_language_reads_as_WiseOwl_reads_it()
    {
        if (!SnoStore.TryLocateInstall(out var install) || !File.Exists(Path.Combine(install, ".build.info"))) return;
        using var storage = Diablo4Storage.Open(install);
        foreach (string locale in new[] { "enUS", "esES", "deDE", "frFR" })
        {
            byte[] bytes;
            try { bytes = storage.Casc.ReadPath($@"Base\StringList-Text-{locale}.dat"); }
            catch (CascException) { continue; } // not downloaded here
            AssertSameAs(StringListCatalog.Parse(bytes, locale), new TextBundle(bytes));
            return;
        }
    }
}
