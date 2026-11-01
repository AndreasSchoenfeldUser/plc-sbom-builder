using System.Text;
using System.Text.Json.Nodes;
using PlcSbom.Core.FieldState;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Output;
using PlcSbom.Core.Sbom;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.FieldState;

public sealed class AsBuiltWriterTests
{
    private static readonly DateTimeOffset FieldDay = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    private static async Task<(AsBuiltResult Result, SbomView View)> AsBuilt()
    {
        var view = (await SampleMachine.Get()).View();
        var result = AsBuiltWriter.Write(view, FieldComparer.Compare(view, SampleMachine.Field()), FieldDay);
        return (result, SbomView.Parse(Encoding.UTF8.GetString(result.Sbom)));
    }

    [Fact]
    public async Task Sample_machine_1042_matches_golden_file()
    {
        Golden.AssertMatches("maschine-xy-1042.cdx.json", (await AsBuilt()).Result.Sbom);
    }

    [Fact]
    public async Task As_built_sbom_passes_schema_and_tr_rules()
    {
        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString((await AsBuilt()).Result.Sbom));

        Assert.True(report.SchemaValid);
        Assert.Equal(0, report.Errors);
    }

    [Fact]
    public async Task Firmware_from_the_field_with_origin_as_found_and_the_projected_value_kept()
    {
        var (result, view) = await AsBuilt();

        var cpu = view.Find("plc-1-fw")!;
        Assert.Equal("V4.6.1", cpu.Version);
        Assert.Equal("as-found", cpu.Own("firmware:origin"));
        Assert.Equal("V4.5", cpu.Own("firmware:projected"));
        Assert.Equal("S V-L9A12345", cpu.Own("device:serialNumber"));
        Assert.Equal("5", cpu.Own("device:hardwareRevision"));
        Assert.DoesNotContain(GapCatalog.FirmwareProjectedOnly, cpu.Gaps);
        Assert.Equal(3, result.FirmwareChanged);

        var terminal = view.Find("roboterzelle-fw-device-1-ethercat-1")!;
        Assert.Equal("V2.3", terminal.Version);
        Assert.DoesNotContain(GapCatalog.FirmwareNotInProject, terminal.Gaps);
        Assert.Contains(GapCatalog.OrderNumberNotInProject, terminal.Gaps); // the field file named none

        // Devices not in the field file keep their projected state.
        Assert.Equal("not-in-project", view.Find("roboterzelle-fw-device-1-ethercat-1-2")!.Own("firmware:origin"));
    }

    [Fact]
    public async Task Cpe_follows_the_version_in_its_version_field()
    {
        var (_, view) = await AsBuilt();

        Assert.Equal("cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:4.6.1:*:*:*:*:*:*:*", view.Find("plc-1-fw")!.Cpe);
    }

    [Fact]
    public async Task Vendor_sbom_of_the_projected_version_is_detached()
    {
        var (result, view) = await AsBuilt();

        var cpu = view.Find("plc-1-fw")!;
        Assert.Null(cpu.InputSbom);
        Assert.False(cpu.HasDistributionHash);
        Assert.Null(cpu.Property("bsi:component:filename"));
        Assert.Contains(GapCatalog.VendorSbomMissing, cpu.Gaps);
        Assert.Contains(GapCatalog.HashUnavailable, cpu.Gaps);
        Assert.Empty(view.DependsOn["plc-1-fw"]);
        Assert.Equal("unknown", view.Completeness["plc-1-fw"]);
        Assert.Null(view.Find("vendor-siemens-cpu1214c-v4-5-synthetisch/rtos"));
        Assert.DoesNotContain(view.DependsOn.Keys, k => k.StartsWith("vendor-", StringComparison.Ordinal));
        Assert.Equal("AS_BUILT_VENDOR_SBOM_DETACHED", Assert.Single(result.Warnings).Code);
    }

    [Fact]
    public async Task Same_version_in_the_field_keeps_the_vendor_sbom()
    {
        var view = (await SampleMachine.Get()).View();
        var field = FieldStateReader.Parse(Encoding.UTF8.GetBytes("""
            { "machine": "Maschine XY", "serialNumber": "1043", "date": "2026-10-23",
              "devices": [ { "position": "S7-1200 station_1/0/1", "firmware": "4.5" } ] }
            """), "f.json");

        var result = AsBuiltWriter.Write(view, FieldComparer.Compare(view, field), FieldDay);

        var asBuilt = SbomView.Parse(Encoding.UTF8.GetString(result.Sbom));
        Assert.Empty(result.Warnings);
        Assert.Equal(0, result.FirmwareChanged);
        Assert.NotNull(asBuilt.Find("plc-1-fw")!.InputSbom);
        Assert.NotNull(asBuilt.Find("vendor-siemens-cpu1214c-v4-5-synthetisch/rtos"));
        Assert.Equal("as-found", asBuilt.Find("plc-1-fw")!.Own("firmware:origin"));
    }

    [Fact]
    public async Task Machine_gets_serial_number_field_source_and_a_new_sbom_uri()
    {
        var (_, view) = await AsBuilt();

        var machine = view.Primary!;
        Assert.Equal("1042", machine.Own("machine:serialNumber"));
        Assert.Equal("samples/maschine-xy/feld/1042.json", machine.Own("field:source"));
        Assert.Equal("2026-10-22", machine.Own("field:date"));
        var expected = Uuid5.Create(Uuid5.Namespace, string.Join('\n', "Maschine XY", "R2026.10", "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "1042"));
        Assert.Equal($"urn:uuid:{expected:D}", view.SerialNumber);
        Assert.Equal(FieldDay, view.Timestamp);
    }

    [Theory]
    [InlineData("cpe:2.3:o:siemens:x_firmware:4.5:*:*:*:*:*:*:*", "V4.5", "V4.6.1", "cpe:2.3:o:siemens:x_firmware:4.6.1:*:*:*:*:*:*:*")]
    [InlineData("cpe:2.3:o:siemens:x_firmware:*:*:*:*:*:*:*:*", "V4.5", "V4.6.1", null)]
    [InlineData("cpe:/o:siemens:x_firmware:4.5", "V4.5", "V4.6", null)]
    [InlineData("cpe:2.3:o:a\\:b:x:1.0:*:*:*:*:*:*:*", "1.0", "1.1", "cpe:2.3:o:a\\:b:x:1.1:*:*:*:*:*:*:*")]
    public void Cpe_version_is_replaced_only_where_the_old_version_stands(string cpe, string old, string found, string? expected)
    {
        Assert.Equal(expected, AsBuiltWriter.ReplaceCpeVersion(cpe, old, found));
    }

    [Fact]
    public async Task Cpe_without_the_old_version_is_removed_with_a_warning()
    {
        var built = await SampleMachine.Get();
        var bom = JsonNode.Parse(built.Json)!.AsObject();
        var cpu = bom["components"]!.AsArray().OfType<JsonObject>().Single(c => c["bom-ref"]!.GetValue<string>() == "plc-1-fw");
        cpu["cpe"] = "cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:*:*:*:*:*:*:*:*";
        var view = SbomView.From(bom);

        var result = AsBuiltWriter.Write(view, FieldComparer.Compare(view, SampleMachine.Field()), FieldDay);

        Assert.Null(SbomView.Parse(Encoding.UTF8.GetString(result.Sbom)).Find("plc-1-fw")!.Cpe);
        Assert.Contains(result.Warnings, w => w.Code == "AS_BUILT_CPE_REMOVED");
    }

    [Fact]
    public async Task Two_runs_give_identical_bytes()
    {
        Assert.Equal((await AsBuilt()).Result.Sbom, (await AsBuilt()).Result.Sbom);
    }
}
