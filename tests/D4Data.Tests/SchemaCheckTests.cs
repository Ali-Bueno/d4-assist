using System.Buffers.Binary;
using System.Text;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Tests;

public class SchemaCheckTests
{
    // A root type of 24 bytes: its SNO id at 0, an inline array at 8 and one stored in the Payload file at 32.
    private const string Definitions = """
        {
          "100": { "name": "DT_VARIABLEARRAY", "flags": 0 },
          "200": { "name": "DT_INT", "flags": 0 },
          "300": { "name": "TestDefinition", "size": 24, "snoGroup": 7, "flags": 0, "fields": [
            { "name": "dwSnoId", "offset": 0, "flags": 0, "type": [200] },
            { "name": "arItems", "offset": 8, "flags": 0, "type": [100, 200] },
            { "name": "arPayloadItems", "offset": 32, "flags": 2097152, "type": [100, 200] } ] }
        }
        """;

    private static readonly TypeSchema Schema = TypeSchema.Load(new MemoryStream(Encoding.UTF8.GetBytes(Definitions)));

    // The record body: id, then the arItems descriptor { i64 pad; i32 dataOffset; i32 dataSize }, then its data.
    private static SnoBlob Record(int id, int dataOffset, int dataSize, int length = 40)
    {
        var bytes = new byte[Math.Max(length, 24)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, id);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), dataOffset);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), dataSize);
        return new SnoBlob(bytes[..length]);
    }

    private static SchemaCheckResult Check(params (int Id, SnoBlob Meta)[] records) =>
        SchemaCheck.Check(Schema, records.Select(r => ("TestDefinition", r.Id, r.Meta)));

    [Fact]
    public void Records_that_fit_the_schema_pass_and_the_payload_array_is_not_read_here()
    {
        var result = Check((5, Record(5, dataOffset: 24, dataSize: 16)), (6, Record(6, 0, 0)));

        Assert.True(result.Passed);
        Assert.Equal(2, result.Records);
    }

    [Theory]
    [InlineData(24, 32)] // past the record's end
    [InlineData(8, 8)]   // inside the type's own fields
    [InlineData(24, -4)]
    public void An_array_pointing_outside_the_record_fails(int dataOffset, int dataSize)
    {
        var result = Check((5, Record(5, dataOffset, dataSize)));

        Assert.False(result.Passed);
        Assert.Contains("arItems", Assert.Single(result.Failures));
    }

    [Fact]
    public void A_record_shorter_than_its_type_or_not_starting_with_its_id_fails()
    {
        Assert.Contains("bytes", Assert.Single(Check((5, Record(5, 0, 0, length: 20))).Failures));
        Assert.Contains("SNO id", Assert.Single(Check((5, Record(9, 0, 0))).Failures));
    }

    [Fact]
    public void Nothing_read_is_no_pass_and_a_missing_type_is_a_failure()
    {
        Assert.False(Check().Passed);

        var result = SchemaCheck.Check(Schema, [("NoSuchDefinition", 5, Record(5, 0, 0))]);
        Assert.Contains("NoSuchDefinition", Assert.Single(result.Failures));
    }

    [Fact]
    public void Inline_arrays_leave_out_the_payload_ones()
    {
        Assert.Equal([("arItems", 8, false)], Schema.InlineArrayFields("TestDefinition"));
    }
}
