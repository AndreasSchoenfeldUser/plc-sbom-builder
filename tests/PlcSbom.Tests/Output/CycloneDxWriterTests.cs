using System.Text;
using System.Text.Json.Nodes;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Output;

public sealed class CycloneDxWriterTests
{
    private static readonly WriterOptions Options = new() { Timestamp = HandModel.Timestamp, ToolVersion = "0.1.0" };

    private static byte[] WriteHandModel() => CycloneDxWriter.Write(GapRules.Apply(HandModel.Create()), Options);

    private static JsonObject Parse(byte[] bytes) => JsonNode.Parse(bytes)!.AsObject();

    private static JsonObject Component(JsonObject bom, string bomRef) =>
        bom["components"]!.AsArray().OfType<JsonObject>().Single(c => c["bom-ref"]!.GetValue<string>() == bomRef);

    private static List<(string Name, string Value)> Props(JsonObject component) =>
        component["properties"]!.AsArray().OfType<JsonObject>()
            .Select(p => (p["name"]!.GetValue<string>(), p["value"]!.GetValue<string>())).ToList();

    [Fact]
    public void Hand_model_matches_the_golden_file()
    {
        Golden.AssertMatches("hand-model.cdx.json", WriteHandModel());
    }

    [Fact]
    public void Station_gap_without_program_and_controller_goes_to_the_machine()
    {
        // A TwinCAT station before M9: devices without controller flag, a library, no program.
        var station = new Station
        {
            Name = "Zelle",
            AdapterId = "twincat",
            SourcePath = "z.tsproj",
            Devices = [new Device { TypeName = "EK1100" }],
            Software = [new SoftwareItem { Kind = SoftwareKind.Library, Name = "Tc2_Standard", Version = "1.0" }],
            Gaps = [new Gap(GapCatalog.DevicesNotRead, "x")],
        };
        var machine = HandModel.Create() with { Stations = [station], UnassignedDevices = [] };

        var bom = Parse(CycloneDxWriter.Write(GapRules.Apply(machine), Options));

        Assert.Contains(("plcsbom:gap", "devices-not-read"), Props(bom["metadata"]!["component"]!.AsObject()));
        Assert.DoesNotContain(("plcsbom:gap", "devices-not-read"), bom["components"]!.AsArray().OfType<JsonObject>().SelectMany(Props));
    }

    [Fact]
    public void Output_is_byte_identical_across_runs()
    {
        Assert.Equal(WriteHandModel(), WriteHandModel());
    }

    [Fact]
    public void Output_is_utf8_without_bom_with_lf_only()
    {
        var bytes = WriteHandModel();

        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
    }

    [Fact]
    public async Task Hand_model_passes_schema_and_tr_rules_without_errors()
    {
        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(WriteHandModel()));

        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Equal(0, report.ExitCode(releaseBuild: false));
        Assert.Equal(1, report.ExitCode(releaseBuild: true));
        Assert.Equal(2, report.Stations);
    }

    [Fact]
    public void Only_software_components_are_written()
    {
        var bom = Parse(WriteHandModel());

        var types = bom["components"]!.AsArray().Select(c => c!["type"]!.GetValue<string>()).Distinct().Order().ToList();
        Assert.Equal(["application", "firmware", "library"], types);
        Assert.Equal("device", bom["metadata"]!["component"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Passive_devices_and_racks_are_not_components()
    {
        var bom = Parse(WriteHandModel());

        Assert.DoesNotContain(bom["components"]!.AsArray(), c => c!["name"]!.GetValue<string>().StartsWith("Rack", StringComparison.Ordinal));
    }

    [Fact]
    public void Controller_firmware_carries_device_properties_and_projected_origin()
    {
        var fw = Component(Parse(WriteHandModel()), "plc-1-fw");
        var props = Props(fw);

        Assert.Equal("CPU 1511-1 PN, Firmware", fw["name"]!.GetValue<string>());
        Assert.Equal("V2.8", fw["version"]!.GetValue<string>());
        Assert.Contains(("plcsbom:device:orderNumber", "6ES7 511-1AK02-0AB0"), props);
        Assert.Contains(("plcsbom:device:vendor:origin", "assumed"), props);
        Assert.Contains(("plcsbom:firmware:origin", "projected"), props);
        Assert.Contains(("plcsbom:station", "PLC_1"), props);
        Assert.Contains(("plcsbom:adapter", "tia-aml"), props);
        Assert.Contains(("plcsbom:gap", "firmware-projected-only"), props);
        Assert.Contains(("bsi:component:executable", "executable"), props);
        Assert.Contains(("bsi:component:archive", "archive"), props);
        Assert.Contains(("bsi:component:structured", "unstructured"), props);
    }

    [Fact]
    public void Firmware_missing_in_the_project_gets_no_version_but_a_gap()
    {
        var fw = Component(Parse(WriteHandModel()), "plc-1-fw-0-0");

        Assert.Null(fw["version"]);
        Assert.Contains(("plcsbom:gap", "firmware-not-in-project"), Props(fw));
        Assert.Contains(("plcsbom:firmware:origin", "not-in-project"), Props(fw));
    }

    [Fact]
    public void Placeholder_version_is_not_written_as_version()
    {
        var lib = Component(Parse(WriteHandModel()), "roboterzelle-lib-tc2-standard");

        Assert.Null(lib["version"]);
        Assert.Contains(("plcsbom:version:requested", "*"), Props(lib));
        Assert.Contains(("plcsbom:gap", "version-unresolved"), Props(lib));
    }

    [Fact]
    public void Program_depends_on_controller_firmware_and_libraries()
    {
        var bom = Parse(WriteHandModel());
        var app = bom["dependencies"]!.AsArray().OfType<JsonObject>().Single(d => d["ref"]!.GetValue<string>() == "plc-1-app");

        Assert.Equal(["plc-1-fw", "plc-1-lib-lgf"], app["dependsOn"]!.AsArray().Select(x => x!.GetValue<string>()).ToList());
    }

    [Fact]
    public void Every_component_has_a_dependency_entry_and_a_completeness()
    {
        var bom = Parse(WriteHandModel());
        var refs = bom["components"]!.AsArray().Select(c => c!["bom-ref"]!.GetValue<string>()).Append("maschine-xy").ToHashSet();
        var entries = bom["dependencies"]!.AsArray().Select(d => d!["ref"]!.GetValue<string>()).ToHashSet();
        var covered = bom["compositions"]!.AsArray().SelectMany(c => c!["dependencies"]!.AsArray()).Select(x => x!.GetValue<string>()).ToList();

        Assert.Equal(refs, entries);
        Assert.Equal(refs.Order(), covered.Order());
    }

    [Fact]
    public void Program_without_blocks_is_marked_incomplete()
    {
        var bom = Parse(WriteHandModel());
        var incomplete = bom["compositions"]!.AsArray().OfType<JsonObject>().Single(c => c["aggregate"]!.GetValue<string>() == "incomplete");

        Assert.Equal(["plc-1-app"], incomplete["dependencies"]!.AsArray().Select(x => x!.GetValue<string>()).ToList());
    }

    [Fact]
    public void Serial_number_is_stable_per_machine_release_and_commit()
    {
        var a = Parse(WriteHandModel())["serialNumber"]!.GetValue<string>();
        var other = CycloneDxWriter.Write(HandModel.Create() with { Release = "R2026.11" }, Options);

        Assert.StartsWith("urn:uuid:", a);
        Assert.NotEqual(a, Parse(other)["serialNumber"]!.GetValue<string>());
    }

    [Fact]
    public void Namespace_comes_from_the_options()
    {
        var bytes = CycloneDxWriter.Write(GapRules.Apply(HandModel.Create()), Options with { Namespace = "firma" });
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"firma:station\"", text);
        Assert.DoesNotContain("plcsbom:", text);
    }

    [Theory]
    [InlineData("cdx")]
    [InlineData("bsi")]
    [InlineData("")]
    public void Reserved_namespaces_are_refused(string ns)
    {
        Assert.Throws<ArgumentException>(() => CycloneDxWriter.Write(HandModel.Create(), Options with { Namespace = ns }));
    }

    [Fact]
    public void A_hash_without_uri_is_a_model_error()
    {
        var machine = HandModel.Create();
        var station = machine.Stations[0] with
        {
            Software = [machine.Stations[0].Software[0] with { SourceUri = null }],
        };

        Assert.Throws<InvalidOperationException>(() => CycloneDxWriter.Write(machine with { Stations = [station] }, Options));
    }

    [Fact]
    public void Distribution_and_original_licence_are_written_as_two_licences()
    {
        var machine = HandModel.Create() with { Licensing = new Licensing("MIT", Original: "Apache-2.0") };
        var licenses = Parse(CycloneDxWriter.Write(machine, Options))["metadata"]!["component"]!["licenses"]!.AsArray();

        Assert.Equal("MIT", licenses[0]!["license"]!["id"]!.GetValue<string>());
        Assert.Equal("concluded", licenses[0]!["license"]!["acknowledgement"]!.GetValue<string>());
        Assert.Equal("declared", licenses[1]!["license"]!["acknowledgement"]!.GetValue<string>());
    }

    [Fact]
    public void Network_data_never_reaches_the_sbom()
    {
        var text = Encoding.UTF8.GetString(WriteHandModel());

        Assert.DoesNotMatch(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", text);
    }
}
