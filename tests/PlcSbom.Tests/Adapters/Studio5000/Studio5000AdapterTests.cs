using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.Studio5000;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Adapters.Studio5000;

public sealed class Studio5000AdapterTests
{
    private const string Cicd = "studio5000/ra-ExampleForCICD_L85E.L5X";
    private const string WithAoi = "studio5000/acd-ACDTestsWithAOI.L5X";
    private const string Redundant = "studio5000/acd-ACDTestsEmptyRedundant.L5X";
    private const string Tree = "studio5000/module-baum-synthetisch.L5X";

    private static Station Read(string fixture, string station = "Abfuellung") =>
        Studio5000Adapter.Read(Fixtures.PathOf(fixture), new Studio5000StationOptions
        {
            StationName = station,
            SourcePath = "rockwell/" + Path.GetFileName(fixture),
        });

    /// <summary>Machine around one adapter station, as build (M9) will assemble it; machine data from the hand model.</summary>
    private static byte[] Sbom(Station station)
    {
        var machine = new Machine
        {
            Name = "Maschine XY",
            Release = "R2026.10",
            Commit = HandModel.Commit,
            Supplier = new Party("Musterbau GmbH", Email: "sbom@musterbau.example"),
            Licensing = new Licensing("LicenseRef-musterbau-proprietary", Effective: "LicenseRef-musterbau-proprietary"),
            Stations = [station],
        };
        return CycloneDxWriter.Write(GapRules.Apply(machine), new WriterOptions { Timestamp = HandModel.Timestamp, ToolVersion = "0.1.0" });
    }

    [Fact]
    public void Station_carries_adapter_hash_and_engineering_tool()
    {
        var station = Read(Cicd);

        Assert.Equal(AdapterIds.Studio5000, station.AdapterId);
        Assert.Null(station.AdapterSource);
        Assert.Equal("rockwell/ra-ExampleForCICD_L85E.L5X", station.SourcePath);
        Assert.Equal("d39622b04aa02c0c152faf02c076cea0f06aff51a1019e75b2e1e2813245555e", station.SourceSha256);
        Assert.Equal(new ToolInfo("Studio 5000 Logix Designer", "36.00"), station.EngineeringTool);
        Assert.Null(station.SourceExportedAt); // ExportDate has no time zone
    }

    [Fact]
    public void Controller_firmware_is_the_project_revision()
    {
        var controller = Assert.Single(Read(Cicd).Devices);

        Assert.True(controller.IsController);
        Assert.Equal("1756-L85E", controller.TypeName);
        Assert.Equal("1756-L85E", controller.OrderNumber);
        Assert.Equal("Local", controller.DeviceName);
        Assert.Equal("Local/0", controller.Position);
        Assert.Equal("36.11", controller.Firmware);
        Assert.Equal(FirmwareOrigin.Projected, controller.FirmwareOrigin);
        Assert.Equal("Rockwell Automation", controller.Vendor!.Name);
        Assert.Equal(ValueOrigin.Assumed, controller.VendorOrigin);
        Assert.Equal(
            new Dictionary<string, string> { ["ProductCode"] = "168", ["ProductType"] = "14", ["VendorId"] = "1" },
            controller.VendorKeys);
        Assert.Equal("Disabled", controller.SourceProperties["device:ekey"]);
        Assert.Empty(controller.Gaps); // keying of the controller module is no gap (E-017)
    }

    [Fact]
    public void Module_in_the_chassis_has_slot_revision_and_keying()
    {
        var en2t = Read(WithAoi).Devices.Single(d => d.DeviceName == "ETH_MODULE");

        Assert.False(en2t.IsController);
        Assert.Equal("1756-EN2T", en2t.OrderNumber);
        Assert.Equal("Local/1", en2t.Position);
        Assert.Equal("11.1", en2t.Firmware);
        Assert.Equal(FirmwareOrigin.Projected, en2t.FirmwareOrigin);
        Assert.Equal("CompatibleModule", en2t.SourceProperties["device:ekey"]);
        Assert.Empty(en2t.Gaps);
    }

    [Fact]
    public void Controller_comes_first_then_its_modules_depth_first()
    {
        var devices = Read(Tree).Devices;

        Assert.Equal(
            ["Local", "ENET_Lokal", "Remote_Rack", "DI_Remote", "Waage", "Ohne_Revision"],
            devices.Select(d => d.DeviceName));
        Assert.Equal(
            ["Local/0", "Local/2", "ENET_Lokal/Remote_Rack", "Remote_Rack/3", "ENET_Lokal/Waage", "Local/4"],
            devices.Select(d => d.Position));
    }

    [Fact]
    public void Disabled_keying_on_a_module_is_a_warning_gap()
    {
        var di = Read(Tree).Devices.Single(d => d.DeviceName == "DI_Remote");

        var gap = Assert.Single(di.Gaps);
        Assert.Equal(GapCatalog.EkeyDisabled, gap.Code);
        Assert.Contains("3.1", gap.Message);
        Assert.Equal(Severity.Warning, GapCatalog.Find(gap.Code)!.Severity);
    }

    [Fact]
    public void Other_vendor_id_gives_no_vendor_name()
    {
        var waage = Read(Tree).Devices.Single(d => d.DeviceName == "Waage");

        Assert.Null(waage.Vendor);
        Assert.Equal("65000", waage.VendorKeys["VendorId"]);
        Assert.Equal("2.4", waage.Firmware);
        Assert.Contains("65000", Assert.Single(waage.Gaps, g => g.Code == GapCatalog.SupplierUnknown).Message);
    }

    [Fact]
    public void Module_without_revision_has_no_firmware()
    {
        var module = Read(Tree).Devices.Single(d => d.DeviceName == "Ohne_Revision");

        Assert.Null(module.Firmware);
        Assert.Equal(FirmwareOrigin.NotInProject, module.FirmwareOrigin);
    }

    [Fact]
    public void Aois_become_add_on_instructions_with_revision()
    {
        var aoi = Assert.Single(Read(WithAoi).Software);

        Assert.Equal(SoftwareKind.AddOnInstruction, aoi.Kind);
        Assert.Equal("AddOnInstruction", aoi.Name);
        Assert.Equal("1.7", aoi.Version);
        Assert.True(aoi.VersionResolved);
        Assert.Equal("ACD-TOOLS", aoi.Vendor!.Name);
        Assert.Equal(ValueOrigin.Source, aoi.VendorOrigin);
        Assert.Equal("ExtendedText", aoi.SourceProperties["aoi:revisionExtension"]);
    }

    [Fact]
    public void Aoi_without_vendor_gets_supplier_unknown()
    {
        var station = GapRules.Apply(new Machine
        {
            Name = "M",
            Release = "R1",
            Supplier = new Party("Musterbau GmbH", Email: "sbom@musterbau.example"),
            Stations = [Read(Tree)],
        }).Stations[0];

        var ventil = station.Software.Single(s => s.Name == "Ventil_Einfach");
        Assert.Null(ventil.Vendor);
        Assert.Contains(ventil.Gaps, g => g.Code == GapCatalog.SupplierUnknown);
        Assert.False(ventil.SourceProperties.ContainsKey("aoi:revisionExtension"));
    }

    [Theory]
    [InlineData("studio5000/ra-WetBulbTemperature.L5X")]
    [InlineData("studio5000/ra-DelayedSum_AOI.L5X")]
    public void Component_export_is_refused(string fixture)
    {
        var ex = Assert.Throws<UsageException>(() => Read(fixture));

        Assert.Equal("STUDIO5000_NOT_CONTROLLER", ex.Code);
        Assert.Contains("AddOnInstructionDefinition", ex.Message);
    }

    [Fact]
    public void Missing_file_ends_with_exit_2()
    {
        Assert.Equal("SOURCE_NOT_FOUND", Assert.Throws<UsageException>(() => Read("studio5000/gibt-es-nicht.L5X")).Code);
    }

    [Theory]
    [InlineData(Cicd, "studio5000-ra-l85e.cdx.json")]
    [InlineData(WithAoi, "studio5000-acd-aoi.cdx.json")]
    [InlineData(Tree, "studio5000-module-baum.cdx.json")]
    public void Sbom_matches_the_golden_file(string fixture, string golden)
    {
        Golden.AssertMatches(golden, Sbom(Read(fixture)));
    }

    [Theory]
    [InlineData(Cicd)]
    [InlineData(WithAoi)]
    [InlineData(Redundant)]
    [InlineData(Tree)]
    public async Task Sbom_passes_schema_and_tr_rules_without_errors(string fixture)
    {
        var bytes = Sbom(Read(fixture));

        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(bytes));

        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Equal(bytes, Sbom(Read(fixture)));
    }

    [Theory]
    [InlineData(WithAoi, "192.168.1.2")]
    [InlineData(Tree, "192.0.2.")]
    [InlineData(Cicd, "127.0.0.1")]
    public void Sbom_contains_no_network_addresses(string fixture, string address)
    {
        Assert.Contains(address, Encoding.UTF8.GetString(Fixtures.Read(fixture)));
        Assert.DoesNotContain(address, Encoding.UTF8.GetString(Sbom(Read(fixture))));
    }

    [Fact]
    public void Sbom_writes_rockwell_properties()
    {
        var json = Encoding.UTF8.GetString(Sbom(Read(Tree)));

        Assert.Contains("\"name\": \"plcsbom:device:ekey\",\n          \"value\": \"ExactMatch\"", json);
        Assert.Contains("\"name\": \"plcsbom:device:vendor:origin\",\n          \"value\": \"assumed\"", json);
        Assert.Contains("\"name\": \"plcsbom:vendorKey:ProductCode\"", json);
        Assert.Contains("\"name\": \"plcsbom:aoi:revisionExtension\",\n          \"value\": \"Werksstand\"", json);
        Assert.Contains("\"bom-ref\": \"abfuellung-aoi-comquent-motor\"", json);
        Assert.Contains("\"name\": \"plcsbom:gap\",\n          \"value\": \"ekey-disabled\"", json);
    }
}
