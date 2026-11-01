using System.Text.Json.Nodes;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Merge;
using PlcSbom.Core.Model;
using PlcSbom.Core.Validation;

namespace PlcSbom.Tests.Merge;

public sealed class InputSbomReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("plc-sbom-input-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Syft => RepoPaths.Of("samples", "maschine-xy", "sbom", "teil", "hmi-web.cdx.json");

    private static string Vendor => RepoPaths.Of("samples", "maschine-xy", "sbom", "lieferanten", "siemens-cpu1214c-v4.5-synthetisch.cdx.json");

    private Task<InputSbomRead> ReadText(string json, InputSbomKind kind = InputSbomKind.Partial)
    {
        var path = Path.Combine(_dir, "eingang.cdx.json");
        File.WriteAllText(path, json);
        return InputSbomReader.ReadAsync(path, "eingang.cdx.json", kind, "in", station: null);
    }

    private static string Ref(JsonObject component) => component["bom-ref"]!.ToString();

    [Fact]
    public async Task Syft_sbom_gets_prefixed_refs_and_a_root_that_holds_its_package()
    {
        var read = await InputSbomReader.ReadAsync(Syft, "teil/hmi-web.cdx.json", InputSbomKind.Partial, "partial-hmi-web", "PLC_1");
        var sbom = read.Sbom!;

        Assert.Equal("partial-hmi-web/af63bd4c8601b7f1", Ref(sbom.Root));
        Assert.All(sbom.Components, c => Assert.StartsWith("partial-hmi-web/pkg:npm/", Ref(c)));
        // Only the package nothing else depends on hangs under the root; ms and picocolors stay below it.
        Assert.Equal(["partial-hmi-web/pkg:npm/maschine-xy-hmi-web@1.4.0?package-id=2ca70c872f5ed6a5"], sbom.DependsOn[Ref(sbom.Root)]);
        Assert.Equal(2, sbom.DependsOn["partial-hmi-web/pkg:npm/maschine-xy-hmi-web@1.4.0?package-id=2ca70c872f5ed6a5"].Count);
        Assert.Equal("PLC_1", sbom.Station);
        Assert.Equal("syft", read.ToolName);
    }

    [Fact]
    public async Task Syft_sbom_is_not_tr_conform_and_its_components_are_marked()
    {
        var read = await InputSbomReader.ReadAsync(Syft, "teil/hmi-web.cdx.json", InputSbomKind.Partial, "p", null);

        var warning = Assert.Single(read.Findings);
        Assert.Equal("PARTIAL_SBOM_NOT_TR_CONFORM", warning.Code);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(4, read.Sbom!.Gaps.Count);
        Assert.All(read.Sbom.Gaps.Values, codes => Assert.Equal([GapCatalog.InputFieldMissing], codes));
    }

    [Fact]
    public async Task Complete_vendor_sbom_needs_no_gap_and_gives_its_key()
    {
        var read = await InputSbomReader.ReadAsync(Vendor, "lieferanten/x.cdx.json", InputSbomKind.Vendor, "v", null);

        Assert.Empty(read.Findings);
        Assert.Empty(read.Sbom!.Gaps);
        Assert.Equal(new VendorSbomKey("Siemens AG", "6ES7 214-1AG40-0XB0", "V4.5", null), read.Key);
        Assert.Equal("complete", read.Sbom.Completeness["v/fw"]);
        Assert.Equal(["v/rtos", "v/tcpip"], read.Sbom.DependsOn["v/fw"]);
    }

    [Fact]
    public async Task Effective_licence_is_removed_because_this_sbom_sets_it_anew()
    {
        var read = await InputSbomReader.ReadAsync(Vendor, "x.cdx.json", InputSbomKind.Vendor, "v", null);

        Assert.DoesNotContain(read.Sbom!.Root["properties"]!.AsArray(), p => p!["name"]!.ToString().StartsWith("bsi:component:effectiveLicen", StringComparison.Ordinal));
        Assert.Contains(read.Sbom.Root["properties"]!.AsArray(), p => p!["name"]!.ToString() == "bsi:component:filename");
    }

    [Fact]
    public async Task Components_without_bom_ref_get_one_and_nested_ones_are_prefixed()
    {
        var read = await ReadText("""
            { "bomFormat": "CycloneDX", "specVersion": "1.6",
              "metadata": { "component": { "type": "application", "name": "app" } },
              "components": [ { "bom-ref": "a", "type": "library", "name": "a",
                                "components": [ { "bom-ref": "a1", "type": "library", "name": "a1" } ] },
                              { "type": "library", "name": "b" } ] }
            """);
        var sbom = read.Sbom!;

        Assert.Equal("in/component-1", Ref(sbom.Root));
        Assert.Equal(["in/a", "in/component-2"], sbom.Components.Select(Ref));
        Assert.Equal("in/a1", ((JsonObject)sbom.Components[0]["components"]![0]!)["bom-ref"]!.ToString());
        Assert.Equal(["in/a", "in/component-2"], sbom.DependsOn["in/component-1"]);
    }

    [Fact]
    public async Task Unknown_refs_in_dependencies_are_dropped_with_a_warning()
    {
        var read = await ReadText("""
            { "bomFormat": "CycloneDX", "specVersion": "1.6",
              "metadata": { "component": { "bom-ref": "app", "type": "application", "name": "app" } },
              "components": [ { "bom-ref": "a", "type": "library", "name": "a" } ],
              "dependencies": [ { "ref": "app", "dependsOn": [ "a", "fehlt" ] }, { "ref": "gibt-es-nicht", "dependsOn": [] } ] }
            """);

        Assert.Contains(read.Findings, f => f.Code == "INPUT_SBOM_REF_UNKNOWN" && f.Message.Contains("2 bom-ref(s)", StringComparison.Ordinal));
        Assert.Equal(["in/a"], read.Sbom!.DependsOn["in/app"]);
    }

    [Theory]
    [InlineData("{ nicht json", "kein gültiges JSON")]
    [InlineData("[]", "kein gültiges JSON")]
    [InlineData("""{ "bomFormat": "SPDX" }""", "keine CycloneDX-SBOM")]
    [InlineData("""{ "bomFormat": "CycloneDX", "specVersion": "1.6", "components": [] }""", "keine metadata.component")]
    [InlineData("""{ "bomFormat": "CycloneDX", "specVersion": "1.6", "metadata": { "component": { "bom-ref": "x", "type": "application", "name": "x" } }, "components": [ { "bom-ref": "x", "type": "library", "name": "y" } ] }""", "mehrfach")]
    public async Task Unusable_input_is_an_error_and_not_merged(string json, string message)
    {
        var read = await ReadText(json);

        Assert.Null(read.Sbom);
        var finding = Assert.Single(read.Findings);
        Assert.Equal("INPUT_SBOM_INVALID", finding.Code);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Contains(message, finding.Message);
    }

    [Fact]
    public async Task Order_number_property_of_any_namespace_except_cdx_and_bsi_is_the_key()
    {
        var read = await ReadText("""
            { "bomFormat": "CycloneDX", "specVersion": "1.6",
              "metadata": { "component": { "bom-ref": "fw", "type": "firmware", "name": "fw", "version": "1.0",
                "manufacturer": { "name": "Hersteller AG" },
                "properties": [ { "name": "cdx:device:orderNumber", "value": "falsch" }, { "name": "lieferant:device:orderNumber", "value": "4711" } ] } } }
            """, InputSbomKind.Vendor);

        Assert.Equal(new VendorSbomKey("Hersteller AG", "4711", "1.0", null), read.Key);
    }

    [Fact]
    public void Unexplained_missing_fields_respect_gaps_and_logical_components()
    {
        var bare = JsonNode.Parse("""{ "name": "x" }""")!.AsObject();
        var withGap = JsonNode.Parse("""{ "name": "x", "properties": [ { "name": "a:gap", "value": "input-field-missing" } ] }""")!.AsObject();
        var logical = JsonNode.Parse("""{ "name": "x", "version": "1", "properties": [ { "name": "a:logicalComponent", "value": "true" } ] }""")!.AsObject();

        Assert.Equal(8, TrValidator.UnexplainedMissingFields(bare).Count);
        Assert.Empty(TrValidator.UnexplainedMissingFields(withGap));
        Assert.Equal([TrField.Creator, TrField.DistributionLicence], TrValidator.UnexplainedMissingFields(logical));
    }
}
