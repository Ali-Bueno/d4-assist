using D4Data.Casc;
using D4Data.Format;

namespace D4Data.Schema;

/// <summary>What <see cref="SchemaCheck"/> found: how many records it read and the first ones that disagreed with the schema.</summary>
public sealed record SchemaCheckResult(int Records, IReadOnlyList<string> Failures)
{
    public bool Passed => Records > 0 && Failures.Count == 0;
}

/// <summary>
/// Checks definitions.json against the installed game data. The schema carries no build of its own that could be compared
/// (d4data's buildVersion.txt moves on even when definitions.json stays the same), and the SNO header has no format hash,
/// so it reads a sample of records and checks what a shifted layout would break: each record is at least its type's size
/// and starts with its own SNO id, and every array descriptor at the schema's offsets points inside the record.
/// </summary>
public static class SchemaCheck
{
    /// <summary>The root types the app reads.</summary>
    public static readonly string[] AppRootTypes =
    [
        "WorldDefinition", "SceneDefinition", "MarkerSetDefinition", "ActorDefinition", "QuestDefinition",
        "ConversationDefinition", "GlobalDefinition",
    ];

    public const int DefaultSamplesPerType = 16;

    private const int MaxFailuresReported = 5;

    // DT_POLYMORPHIC_VARIABLEARRAY: the element count after the descriptor, one 8-byte type header per element (SnoBlob).
    private const int PolymorphicCount = 16;
    private const int PolymorphicTypeHeaderSize = 8;

    /// <summary>Reads up to <paramref name="samplesPerType"/> records of each root type, spread over its group, and checks them.</summary>
    public static SchemaCheckResult Run(SnoStore store, TypeSchema schema, IReadOnlyList<string>? rootTypes = null, int samplesPerType = DefaultSamplesPerType)
    {
        var types = rootTypes ?? AppRootTypes;
        var missing = types.Where(t => !schema.HasType(t)).Select(t => $"Type {t} missing from definitions.json").ToList();
        return Check(schema, Sample(store, schema, [.. types.Where(schema.HasType)], samplesPerType), missing);
    }

    /// <summary>Checks the given records; <paramref name="knownFailures"/> come first in the result.</summary>
    public static SchemaCheckResult Check(
        TypeSchema schema, IEnumerable<(string RootType, int Id, SnoBlob Meta)> records, IReadOnlyList<string>? knownFailures = null)
    {
        int count = 0;
        var failures = new List<string>(knownFailures ?? []);
        foreach (var (type, id, meta) in records)
        {
            count++;
            if (failures.Count < MaxFailuresReported && Problem(schema, type, id, meta) is { } problem) failures.Add(problem);
        }
        return new SchemaCheckResult(count, failures);
    }

    private static string? Problem(TypeSchema schema, string type, int id, SnoBlob meta)
    {
        int size;
        List<(string Name, int Offset, bool IsPolymorphic)> arrays;
        try { (size, arrays) = (schema.Size(type), [.. schema.InlineArrayFields(type)]); }
        catch (InvalidDataException e) { return e.Message; }

        string record = $"{type} {id}";
        if (meta.Length < size) return $"{record}: {meta.Length} bytes, the type is {size}";
        if (meta.I32(0) != id) return $"{record}: starts with {meta.I32(0)}, not its SNO id";
        foreach (var (name, offset, polymorphic) in arrays)
        {
            var (data, length) = meta.ArrayDescriptor(offset);
            if (data == 0 && length == 0) continue; // empty
            if (data < size || length < 0 || (long)data + length > meta.Length)
                return $"{record}: {name} at +0x{offset:X} points to {data}+{length}, outside {size}..{meta.Length}";
            if (polymorphic && meta.I32(offset + PolymorphicCount) is var elements && (elements < 0 || (long)elements * PolymorphicTypeHeaderSize > length))
                return $"{record}: {name} at +0x{offset:X} holds {elements} elements in {length} bytes";
        }
        return null;
    }

    private static IEnumerable<(string RootType, int Id, SnoBlob Meta)> Sample(SnoStore store, TypeSchema schema, IReadOnlyList<string> rootTypes, int perType)
    {
        foreach (var type in rootTypes)
        {
            int group;
            try { group = schema.SnoGroup(type); }
            catch (InvalidDataException) { continue; } // not a root type: no records to read
            var ids = store.AllEntries(group).Select(e => e.Id).ToList();
            int step = Math.Max(1, ids.Count / perType), taken = 0;
            for (int i = 0; i < ids.Count && taken < perType; i += step)
            {
                SnoBlob? meta;
                try { meta = store.Meta(group, ids[i]); }
                catch (Exception e) when (e is InvalidDataException or IOException or WiseOwl.Casc.CascException) { continue; } // not installed here, or not a meta file
                if (meta is null) continue; // encrypted
                taken++;
                yield return (type, ids[i], meta);
            }
        }
    }
}
