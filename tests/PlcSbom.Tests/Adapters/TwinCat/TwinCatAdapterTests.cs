using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.TwinCat;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Samples;
using static PlcSbom.Tests.Adapters.TwinCat.TwinCatReaderTests;

namespace PlcSbom.Tests.Adapters.TwinCat;

public sealed class TwinCatAdapterTests
{
    private static Station Read(string fixture, string station = "Roboterzelle") =>
        TwinCatAdapter.Read(Fixtures.PathOf(fixture), new TwinCatStationOptions
        {
            StationName = station,
            SourcePath = "twincat/" + Path.GetFileName(fixture),
        });

    private static Machine Machine(Station station) => new()
    {
        Name = "Maschine XY",
        Release = "R2026.10",
        Commit = HandModel.Commit,
        Supplier = new Party("Musterbau GmbH", Email: "sbom@musterbau.example"),
        Licensing = new Licensing("LicenseRef-musterbau-proprietary", Effective: "LicenseRef-musterbau-proprietary"),
        Stations = [station],
    };

    /// <summary>Machine around one adapter station, as build (M9) will assemble it; machine data from the hand model.</summary>
    private static byte[] Sbom(Station station) =>
        CycloneDxWriter.Write(GapRules.Apply(Machine(station)), new WriterOptions { Timestamp = HandModel.Timestamp, ToolVersion = "0.1.0" });

    private static SoftwareItem Library(string fixture, string name) => Read(fixture).Software.Single(s => s.Name == name);

    [Fact]
    public void Station_carries_adapter_hash_and_xae_version()
    {
        var station = Read(HeatPump);

        Assert.Equal(AdapterIds.TwinCat, station.AdapterId);
        Assert.Equal("8770c82a2c3b0138475b64ed6a3136cd1120639c144214c06dc32b0970f44cdb", station.SourceSha256);
        Assert.Equal(new ToolInfo("TwinCAT XAE", "3.1.4022.4"), station.EngineeringTool);
        Assert.Empty(station.Gaps);
    }

    [Fact]
    public void Terminals_have_ethercat_identity_and_no_firmware()
    {
        var devices = Read(HeatPump).Devices;

        Assert.Equal(10, devices.Count);
        var el3204 = devices.Single(d => d.DeviceName == "InpTempSens1 (EL3204-0200)");
        Assert.Equal("EL3204-0200", el3204.TypeName);
        Assert.Null(el3204.OrderNumber);
        Assert.Null(el3204.Firmware);
        Assert.Equal(FirmwareOrigin.NotInProject, el3204.FirmwareOrigin);
        Assert.Equal("Device 1 (EtherCAT)/1/3", el3204.Position);
        Assert.False(el3204.IsController);
        Assert.Equal(
            new Dictionary<string, string> { ["ProductCode"] = "0x0c843052", ["RevisionNo"] = "0x001200c8", ["VendorId"] = "0x00000002" },
            el3204.VendorKeys);
        Assert.Equal([GapCatalog.FirmwareNotInProject, GapCatalog.OrderNumberNotInProject], el3204.Gaps.Select(g => g.Code));
        Assert.Contains("0x001200c8", el3204.Gaps[0].Message);
        Assert.Contains("0x100A", el3204.Gaps[0].Message);
    }

    [Fact]
    public void Vendor_id_2_is_beckhoff_assumed()
    {
        var coupler = Read(HeatPump).Devices[0];

        Assert.Equal("Beckhoff Automation GmbH & Co. KG", coupler.Vendor!.Name);
        Assert.Equal(ValueOrigin.Assumed, coupler.VendorOrigin);
    }

    [Fact]
    public void Disabled_master_marks_its_terminals()
    {
        Assert.All(Read(HeatPump).Devices, d => Assert.Equal("true", d.SourceProperties["device:disabled"]));
        Assert.All(Read(Spt).Devices, d => Assert.False(d.SourceProperties.ContainsKey("device:disabled")));
    }

    [Fact]
    public void Other_vendor_id_gives_no_vendor_name()
    {
        var drive = Read(Synthetic).Devices.Single(d => d.DeviceName == "Drive 3 (IndraDrive MPB20)");

        Assert.Null(drive.Vendor);
        Assert.Equal("0x00000024", drive.VendorKeys["VendorId"]);
        Assert.Equal("Device 1 (EtherCAT)/3", drive.Position);
        Assert.Contains("0x00000024", drive.Gaps.Single(g => g.Code == GapCatalog.SupplierUnknown).Message);
    }

    [Fact]
    public void Terminal_without_revision_has_no_revision_key()
    {
        var end = Read(Spt).Devices.Single(d => d.TypeName == "EL9011");

        Assert.False(end.VendorKeys.ContainsKey("RevisionNo"));
        Assert.DoesNotContain("RevisionNo", end.Gaps[0].Message);
    }

    [Fact]
    public void Runtime_is_assumed_and_has_no_version()
    {
        var runtime = Read(HeatPump).Software.Single(s => s.Kind == SoftwareKind.Runtime);

        Assert.Equal("TwinCAT 3 Runtime", runtime.Name);
        Assert.Null(runtime.Version);
        Assert.Equal(ValueOrigin.Assumed, runtime.Origin);
        Assert.Equal(ValueOrigin.Assumed, runtime.VendorOrigin);
        Assert.Contains("XAE", Assert.Single(runtime.Gaps, g => g.Code == GapCatalog.RuntimeVersionUnknown).Message);
    }

    [Fact]
    public void Star_placeholder_is_unresolved()
    {
        var std = Library(HeatPump, "Tc2_Standard");

        Assert.Equal("*", std.Version);
        Assert.False(std.VersionResolved);
        Assert.Equal("Beckhoff Automation GmbH", std.Vendor!.Name);
        Assert.Equal("default", std.SourceProperties["library:resolution"]);
        Assert.Contains("neueste installierte Version", Assert.Single(std.Gaps).Message);
    }

    [Fact]
    public void Default_resolution_with_version_is_unresolved()
    {
        var visu = Library(HeatPump, "VisuElems");

        Assert.Equal("3.5.6.10", visu.Version);
        Assert.False(visu.VersionResolved);
        Assert.Contains(visu.Gaps, g => g.Code == GapCatalog.VersionUnresolved && g.Message.Contains("Standardauflösung"));
    }

    [Theory]
    [InlineData("VisuElems", "System")]
    [InlineData("visuinputs", "system")]
    public void Company_system_is_no_vendor_in_any_case(string name, string company)
    {
        var lib = Library(HeatPump, name);

        Assert.Null(lib.Vendor);
        Assert.Equal(company, lib.SourceProperties["library:company"]);
        Assert.Contains(lib.Gaps, g => g.Code == GapCatalog.SupplierUnknown);
    }

    [Fact]
    public void Placeholder_resolution_with_version_is_resolved()
    {
        var core = Library(Cognex, "TcoCore");

        Assert.Equal("0.6.0.913", core.Version);
        Assert.True(core.VersionResolved);
        Assert.Equal("redirection", core.SourceProperties["library:resolution"]);
        Assert.Equal("Vortex.Library", core.Vendor!.Name);
        Assert.Empty(core.Gaps);
    }

    [Fact]
    public void Direct_reference_newest_and_star_placeholder_are_one_component()
    {
        var ethercat = Read(TcoCoreProject).Software.Where(s => s.Name == "Tc2_EtherCAT").ToList();

        var lib = Assert.Single(ethercat);
        Assert.Equal("newest", lib.Version);
        Assert.False(lib.VersionResolved);
        Assert.Equal("fixed", lib.SourceProperties["library:resolution"]);
    }

    [Fact]
    public void Resolution_without_reference_is_an_indirect_library()
    {
        var util = Library(TcoCoreProject, "Tc2_Utilities");

        Assert.Equal("3.3.47.0", util.Version);
        Assert.True(util.VersionResolved);
        Assert.Equal("indirect", util.SourceProperties["library:resolution"]);
    }

    [Fact]
    public void Direct_reference_with_version_is_resolved()
    {
        var own = Library(Synthetic, "Comquent_Zelle");

        Assert.Equal("1.4.0.0", own.Version);
        Assert.True(own.VersionResolved);
        Assert.Equal("Comquent GmbH", own.Vendor!.Name);
        Assert.Equal("Comquent_Zelle,1.4.0.0,Comquent GmbH", own.SourceProperties["library:reference"]);
    }

    [Fact]
    public void Libraries_of_all_plc_projects_appear_once()
    {
        var names = Read(TcoCoreProject).Software.Where(s => s.Kind == SoftwareKind.Library).Select(s => s.Name).ToList();

        Assert.Equal(names.Distinct().Count(), names.Count);
        Assert.Contains("TcoCore", names); // only in the test and example projects
    }

    [Fact]
    public void Profinet_devices_and_external_io_are_a_station_gap()
    {
        var cognex = Assert.Single(Read(Cognex).Gaps);
        Assert.Equal(GapCatalog.DevicesNotRead, cognex.Code);
        Assert.Contains("myveryfirstdataman (Profinet an Device 1 (Profinet Controller))", cognex.Message);
        Assert.Empty(Read(Cognex).Devices);

        var synthetic = Assert.Single(Read(Synthetic).Gaps);
        Assert.Contains("Device 2 (EtherCAT).xti", synthetic.Message);
        Assert.Equal(Severity.Warning, GapCatalog.Find(GapCatalog.DevicesNotRead)!.Severity);
    }

    [Fact]
    public void Missing_file_ends_with_exit_2()
    {
        Assert.Equal("SOURCE_NOT_FOUND", Assert.Throws<UsageException>(() => Read("twincat/gibt-es-nicht.tsproj")).Code);
    }

    [Theory]
    [InlineData("0x00000002", "#x00000002")]
    [InlineData("0x0c843052", "#x0C843052")]
    [InlineData("36", "36")]
    public void Hex_values_are_normalised(string expected, string value)
    {
        Assert.Equal(expected, TwinCatAdapter.Hex(value));
    }

    [Theory]
    [InlineData(HeatPump, "twincat-aixocat-heatpump.cdx.json")]
    [InlineData(Spt, "twincat-spt-ethercat-diagnostics.cdx.json")]
    [InlineData(Cognex, "twincat-tcopen-cognex.cdx.json")]
    [InlineData(TcoCoreProject, "twincat-tcopen-core.cdx.json")]
    [InlineData(Synthetic, "twincat-zelle-synthetisch.cdx.json")]
    public void Sbom_matches_the_golden_file(string fixture, string golden)
    {
        Golden.AssertMatches(golden, Sbom(Read(fixture)));
    }

    [Theory]
    [InlineData(HeatPump)]
    [InlineData(Spt)]
    [InlineData(Cognex)]
    [InlineData(TcoCoreProject)]
    [InlineData(Synthetic)]
    public async Task Sbom_passes_schema_and_tr_rules_without_errors(string fixture)
    {
        var bytes = Sbom(Read(fixture));

        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(bytes));

        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Equal(bytes, Sbom(Read(fixture)));
    }

    [Theory]
    [InlineData(HeatPump, "134.61.132.51")]
    [InlineData(Spt, "192.168.8.100")]
    [InlineData(Spt, "SBTN")]
    [InlineData(Cognex, "192.168.4.1")]
    [InlineData(Synthetic, "192.0.2.")]
    public void Sbom_contains_no_network_data(string fixture, string text)
    {
        Assert.Contains(text, Encoding.UTF8.GetString(Fixtures.Read(fixture)));
        Assert.DoesNotContain(text, Encoding.UTF8.GetString(Sbom(Read(fixture))));
    }

    [Fact]
    public void Sbom_writes_twincat_properties()
    {
        var json = Encoding.UTF8.GetString(Sbom(Read(HeatPump)));

        Assert.Contains("\"name\": \"plcsbom:vendorKey:RevisionNo\",\n          \"value\": \"0x00120000\"", json);
        Assert.Contains("\"name\": \"plcsbom:device:disabled\",\n          \"value\": \"true\"", json);
        Assert.Contains("\"name\": \"plcsbom:firmware:origin\",\n          \"value\": \"not-in-project\"", json);
        Assert.Contains("\"bom-ref\": \"roboterzelle-rt\"", json);
        Assert.Contains("\"name\": \"plcsbom:gap\",\n          \"value\": \"runtime-version-unknown\"", json);
    }
}
