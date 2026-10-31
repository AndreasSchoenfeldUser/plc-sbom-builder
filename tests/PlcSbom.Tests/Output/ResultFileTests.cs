using System.Text;
using System.Text.Json.Nodes;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;

namespace PlcSbom.Tests.Output;

/// <summary>Result file &lt;out&gt;.result.json (CLAUDE.md 7.3). Field names are an interface.</summary>
public sealed class ResultFileTests
{
    private static readonly ValidationReport Report = new()
    {
        SpecVersion = "1.6",
        SchemaValid = true,
        Components = 5,
        Stations = 2,
        Findings =
        [
            new(Severity.Error, "HASH_MISSING", "app: Hash fehlt.", "app", "App", ["hash"]),
            new(Severity.Warning, "LICENSE_UNKNOWN", "fw: Keine Lizenz.", "fw", "FW", ["distributionLicence"]),
            new(Severity.Warning, "LICENSE_UNKNOWN", "rt: Keine Lizenz.", "rt", "RT", ["distributionLicence"]),
            new(Severity.Note, "HASH_UNAVAILABLE", "fw: Kein Hash.", "fw", "FW", ["hash"]),
            new(Severity.Note, "LOGICAL_COMPONENT", "m ist logisch.", "m", "M"),
        ],
    };

    private static JsonObject Write(string sbom = "build/sbom-maschine.cdx.json", int exit = 1) =>
        JsonNode.Parse(ResultFile.Write("validate", sbom, Report, exit))!.AsObject();

    [Fact]
    public void Counts_and_status_are_written()
    {
        var result = Write();

        Assert.Equal(1, result["format"]!.GetValue<int>());
        Assert.Equal("validate", result["command"]!.GetValue<string>());
        Assert.Equal(1, result["exitCode"]!.GetValue<int>());
        Assert.True(result["schemaValid"]!.GetValue<bool>());
        Assert.Equal(1, result["errors"]!.GetValue<int>());
        Assert.Equal(2, result["warnings"]!.GetValue<int>());
        Assert.Equal(2, result["notes"]!.GetValue<int>());
        Assert.Equal(5, result["components"]!.GetValue<int>());
        Assert.Equal(2, result["stations"]!.GetValue<int>());
    }

    [Fact]
    public void Field_order_is_fixed()
    {
        var names = Write().Select(p => p.Key).ToList();

        Assert.Equal(
            ["format", "command", "sbom", "exitCode", "schemaValid", "errors", "warnings", "notes", "components", "stations", "gaps", "findings"],
            names);
    }

    [Fact]
    public void Gaps_are_counted_by_gap_code_without_other_findings()
    {
        var gaps = Write()["gaps"]!.AsObject();

        Assert.Equal(["hash-unavailable", "license-unknown"], gaps.Select(p => p.Key).ToList());
        Assert.Equal(1, gaps["hash-unavailable"]!.GetValue<int>());
        Assert.Equal(2, gaps["license-unknown"]!.GetValue<int>());
    }

    [Fact]
    public void Findings_carry_severity_code_component_fields_and_message()
    {
        var first = Write()["findings"]![0]!.AsObject();

        Assert.Equal("error", first["severity"]!.GetValue<string>());
        Assert.Equal("HASH_MISSING", first["code"]!.GetValue<string>());
        Assert.Equal("app", first["component"]!.GetValue<string>());
        Assert.Equal(["hash"], first["fields"]!.AsArray().Select(f => f!.GetValue<string>()).ToList());
        Assert.Equal("app: Hash fehlt.", first["message"]!.GetValue<string>());
        Assert.Equal(5, Write()["findings"]!.AsArray().Count);
    }

    [Fact]
    public void Finding_without_fields_omits_the_field_list()
    {
        var last = Write()["findings"]![4]!.AsObject();

        Assert.Equal("note", last["severity"]!.GetValue<string>());
        Assert.Null(last["fields"]);
    }

    [Fact]
    public void Windows_paths_are_written_with_slashes()
    {
        Assert.Equal("build/sbom-maschine.cdx.json", Write(@"build\sbom-maschine.cdx.json")["sbom"]!.GetValue<string>());
    }

    [Fact]
    public void Output_is_utf8_with_lf_readable_umlauts_and_trailing_newline()
    {
        var bytes = ResultFile.Write("validate", "a.cdx.json", Report with
        {
            Findings = [new(Severity.Warning, "X", "Lücke „ä“", null, null)],
        }, 0);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text);
        Assert.Contains("Lücke „ä“", text);
    }

    [Fact]
    public void Output_is_byte_identical_across_runs()
    {
        Assert.Equal(ResultFile.Write("validate", "a.json", Report, 1), ResultFile.Write("validate", "a.json", Report, 1));
    }
}
