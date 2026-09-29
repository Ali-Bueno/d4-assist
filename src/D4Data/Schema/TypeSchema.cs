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

    private readonly Dictionary<string, TypeDef> _types;

    private TypeSchema(Dictionary<string, TypeDef> types) => _types = types;

    public static TypeSchema Load(string definitionsPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(definitionsPath));
        var types = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
        long polymorphicArrayHash = doc.RootElement.EnumerateObject()
            .Where(e => e.Value.GetProperty("name").GetString() == PolymorphicArrayType).Select(e => long.Parse(e.Name)).FirstOrDefault(-1);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var t = entry.Value;
            var name = t.GetProperty("name").GetString()!;
            var fields = new Dictionary<string, FieldDef>(StringComparer.Ordinal);
            if (t.TryGetProperty("fields", out var fs) && fs.ValueKind == JsonValueKind.Array)
                foreach (var f in fs.EnumerateArray())
                    fields[f.GetProperty("name").GetString()!] = new FieldDef(
                        f.GetProperty("offset").GetInt32(), f.GetProperty("flags").GetInt32(),
                        f.GetProperty("type")[0].GetInt64() == polymorphicArrayHash);
            var size = t.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0;
            var group = t.TryGetProperty("snoGroup", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetInt32() : -1;
            types[name] = new TypeDef(size, group, fields);
        }
        return new TypeSchema(types);
    }

    public int Offset(string type, string field) => Field(type, field).Offset;

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

    private readonly record struct FieldDef(int Offset, int Flags, bool IsPolymorphicArray);
}
