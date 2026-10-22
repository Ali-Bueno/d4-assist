using System.Text.Json;

namespace D4Data.Schema;

/// <summary>Field offsets, type sizes and SNO groups resolved at runtime from d4data's definitions.json.</summary>
/// <remarks>definitions.json is the reflection dump of the game's own type system, regenerated per build
/// (https://github.com/DiabloTools/d4data). Reading offsets from it keeps this tool free of hardcoded layouts.</remarks>
public sealed class TypeSchema
{
    // Field flags that mark a variable array as stored in the SNO's Payload file (d4data parse.js, DT_VARIABLEARRAY).
    private const int ExternalPayloadFlagA = 0x200000;
    private const int ExternalPayloadFlagB = 0x400000;

    // Type name whose hash (the key in definitions.json) marks a field as a polymorphic array.
    private const string PolymorphicArrayType = "DT_POLYMORPHIC_VARIABLEARRAY";
    private const string VariableArrayType = "DT_VARIABLEARRAY";

    private readonly Dictionary<string, TypeDef> _types;
    private readonly Dictionary<uint, string> _namesByHash;

    private TypeSchema(Dictionary<string, TypeDef> types, Dictionary<uint, string> namesByHash) =>
        (_types, _namesByHash) = (types, namesByHash);

    public static TypeSchema Load(string definitionsPath)
    {
        using var file = File.OpenRead(definitionsPath);
        return Load(file);
    }

    /// <summary>Parses a d4data definitions.json from a stream, e.g. a copy embedded in an assembly.</summary>
    public static TypeSchema Load(Stream definitions)
    {
        using var doc = JsonDocument.Parse(definitions);
        var types = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
        var namesByHash = new Dictionary<uint, string>();
        long polymorphicArrayHash = HashOf(doc, PolymorphicArrayType);
        long variableArrayHash = HashOf(doc, VariableArrayType);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var t = entry.Value;
            var name = t.GetProperty("name").GetString()!;
            var fields = new Dictionary<string, FieldDef>(StringComparer.Ordinal);
            if (t.TryGetProperty("fields", out var fs) && fs.ValueKind == JsonValueKind.Array)
                foreach (var f in fs.EnumerateArray())
                {
                    long outer = f.GetProperty("type")[0].GetInt64();
                    fields[f.GetProperty("name").GetString()!] = new FieldDef(
                        f.GetProperty("offset").GetInt32(), f.GetProperty("flags").GetInt32(),
                        outer == polymorphicArrayHash, outer == variableArrayHash);
                }
            var size = t.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0;
            var group = t.TryGetProperty("snoGroup", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetInt32() : -1;
            types[name] = new TypeDef(size, group, fields);
            if (long.TryParse(entry.Name, out long hash)) namesByHash[(uint)hash] = name;
        }
        return new TypeSchema(types, namesByHash);
    }

    private static long HashOf(JsonDocument doc, string typeName) => doc.RootElement.EnumerateObject()
        .Where(e => e.Value.GetProperty("name").GetString() == typeName).Select(e => long.Parse(e.Name)).FirstOrDefault(-1);

    /// <summary>The variable and polymorphic arrays of <paramref name="type"/> whose data sits in the same (Meta) record,
    /// by field name and descriptor offset.</summary>
    public IEnumerable<(string Name, int Offset, bool IsPolymorphic)> InlineArrayFields(string type) =>
        Type(type).Fields
            .Where(f => (f.Value.IsVariableArray || f.Value.IsPolymorphicArray) && (f.Value.Flags & (ExternalPayloadFlagA | ExternalPayloadFlagB)) == 0)
            .OrderBy(f => f.Value.Offset)
            .Select(f => (f.Key, f.Value.Offset, f.Value.IsPolymorphicArray));

    public int Offset(string type, string field) => Field(type, field).Offset;

    /// <summary>A polymorphic array element's type: its dwType holds the type's hash, the key in definitions.json.</summary>
    public string? TypeName(uint typeHash) => _namesByHash.GetValueOrDefault(typeHash);

    public bool HasField(string type, string field) => Type(type).Fields.ContainsKey(field);

    public bool HasType(string type) => _types.ContainsKey(type);

    public int Size(string type) => Type(type).Size;

    /// <summary>The SNO group whose root record is <paramref name="rootType"/> (e.g. WorldDefinition -> World).</summary>
    public int SnoGroup(string rootType) =>
        Type(rootType).SnoGroup is >= 0 and var g ? g : throw new InvalidDataException($"{rootType} is not a SNO root type");

    public bool IsInPayload(string type, string field) =>
        (Field(type, field).Flags & (ExternalPayloadFlagA | ExternalPayloadFlagB)) != 0;

    public bool IsPolymorphicArray(string type, string field) => Field(type, field).IsPolymorphicArray;

    private TypeDef Type(string type) =>
        _types.TryGetValue(type, out var t) ? t : throw new InvalidDataException($"Type {type} missing from definitions.json");

    private FieldDef Field(string type, string field) =>
        Type(type).Fields.TryGetValue(field, out var f) ? f
            : throw new InvalidDataException($"Field {type}.{field} missing from definitions.json");

    private sealed record TypeDef(int Size, int SnoGroup, Dictionary<string, FieldDef> Fields);

    private readonly record struct FieldDef(int Offset, int Flags, bool IsPolymorphicArray, bool IsVariableArray);
}
