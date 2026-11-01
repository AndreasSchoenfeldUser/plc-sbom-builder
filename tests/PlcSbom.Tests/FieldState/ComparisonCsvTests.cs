using System.Text;
using PlcSbom.Core.FieldState;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.FieldState;

public sealed class ComparisonCsvTests
{
    [Fact]
    public async Task Sample_machine_1042_matches_golden_file()
    {
        var comparison = FieldComparer.Compare((await SampleMachine.Get()).View(), SampleMachine.Field());

        Golden.AssertMatches("maschine-xy-1042.abgleich.csv", ComparisonCsv.Write(comparison));
    }

    [Fact]
    public async Task Csv_has_bom_semicolons_lf_and_quotes_where_needed()
    {
        var view = (await SampleMachine.Get()).View();
        var field = FieldStateReader.Parse(Encoding.UTF8.GetBytes("""
            { "machine": "Maschine XY", "serialNumber": "1", "date": "2026-10-22",
              "devices": [ { "station": "PLC_1", "position": "a;b \"c\"", "orderNumber": "X", "firmware": "V1" } ] }
            """), "f.json");

        var bytes = ComparisonCsv.Write(FieldComparer.Compare(view, field));

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.DoesNotContain("\r", text);
        Assert.StartsWith("Station;Position;Gerät;Bestellnummer SBOM;", text);
        Assert.Contains("PLC_1;\"a;b \"\"c\"\"\";X;", text);
    }
}
