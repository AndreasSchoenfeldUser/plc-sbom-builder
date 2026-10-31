using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.Codesys;
using PlcSbom.Core.Adapters.Codesys.V35;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Adapters.Codesys;

public sealed class CodesysV35AdapterTests
{
    private const string Robin = "codesys/v35/robin-codesys_project.xml";
    private const string Osc = "codesys/v35/osc-mqtt-OSC_MQTT.xml";
    private const string Ooip = "codesys/v35/ooip-script-engine-example.xml";
    private const string Synthetic = "codesys/v35/zwei-steuerungen-synthetisch.xml";
    private const string WithDevdesc = "codesys/v35/devdesc-geraete-synthetisch.xml";

    private static Station ReadWithDescriptions() =>
        CodesysV35Adapter.Read(Fixtures.PathOf(WithDevdesc), new CodesysStationOptions
        {
            StationName = "Pruefstand",
            SourcePath = "codesys/devdesc-geraete-synthetisch.xml",
            DeviceDescriptions = DeviceDescriptionIndex.Load([(Fixtures.PathOf("codesys/devdesc"), "codesys/devdesc")]),
        });

    private static Station Read(string fixture, string? plc = null, string station = "Verpackung") =>
        CodesysV35Adapter.Read(Fixtures.PathOf(fixture), new CodesysStationOptions
        {
            StationName = station,
            SourcePath = "codesys/" + Path.GetFileName(fixture),
            Plc = plc,
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
    public void Station_carries_adapter_source_hash_and_engineering_tool()
    {
        var station = Read(Robin);

        Assert.Equal(AdapterIds.Codesys, station.AdapterId);
        Assert.Equal("v35", station.AdapterSource);
        Assert.Equal("codesys/robin-codesys_project.xml", station.SourcePath);
        Assert.Equal("acf12d77f7ad9ba162485e9c8d64545d97ab37e81f396507112917330af17f2d", station.SourceSha256);
        Assert.Equal(new ToolInfo("CODESYS", "V3.5 SP15"), station.EngineeringTool);
        Assert.Null(station.SourceExportedAt); // creationDateTime has no time zone
    }

    [Fact]
    public void Device_description_version_is_not_the_firmware()
    {
        var device = Assert.Single(Read(Robin).Devices);

        Assert.True(device.IsController);
        Assert.Equal("Device", device.DeviceName);
        Assert.Equal("Gerät 4102/0000 0005", device.TypeName);
        Assert.Null(device.Firmware);
        Assert.Equal(FirmwareOrigin.Unknown, device.FirmwareOrigin);
        Assert.Null(device.Vendor);
        Assert.Null(device.OrderNumber);
        Assert.Equal("3.5.15.0", device.VendorKeys["DeviceDescriptionVersion"]);
        Assert.Equal("4102", device.VendorKeys["DeviceType"]);
        Assert.Equal("0000 0005", device.VendorKeys["DeviceId"]);
        Assert.Equal(
            [GapCatalog.FirmwareNotInProject, GapCatalog.OrderNumberNotInProject, GapCatalog.SupplierUnknown],
            device.Gaps.Select(g => g.Code));
        Assert.Contains("Gerätebeschreibung", device.Gaps[0].Message);
    }

    [Fact]
    public void Placeholder_with_default_resolution_only_is_unresolved()
    {
        var sm3 = Read(Robin).Software.Single(s => s.Name == "SM3_Basic");

        Assert.Equal(SoftwareKind.Library, sm3.Kind);
        Assert.Equal("4.5.1.0", sm3.Version);
        Assert.False(sm3.VersionResolved);
        Assert.Equal("3S - Smart Software Solutions GmbH", sm3.Vendor!.Name);
        Assert.Equal("default", sm3.SourceProperties["library:resolution"]);
        Assert.Equal("#SM3_Basic", sm3.SourceProperties["library:reference"]);
        var gap = Assert.Single(sm3.Gaps);
        Assert.Equal(GapCatalog.VersionUnresolved, gap.Code);
        Assert.Contains("Gerätebeschreibung", gap.Message);
    }

    [Fact]
    public void Direct_reference_has_a_resolved_version()
    {
        var robin = Read(Robin).Software.Single(s => s.Name == "Robin");

        Assert.Equal("0.1.2", robin.Version);
        Assert.True(robin.VersionResolved);
        Assert.Equal("INESC TEC", robin.Vendor!.Name);
        Assert.Equal("fixed", robin.SourceProperties["library:resolution"]);
        Assert.Empty(robin.Gaps);
    }

    [Fact]
    public void Redirected_placeholder_has_a_resolved_version()
    {
        var standard = Read(Ooip).Software.Single(s => s.Name == "Standard");

        Assert.Equal("3.5.18.0", standard.Version);
        Assert.True(standard.VersionResolved);
        Assert.Equal("redirection", standard.SourceProperties["library:resolution"]);
    }

    [Fact]
    public void Star_version_is_unresolved()
    {
        var bplog = Read(Ooip).Software.Single(s => s.Name == "Breakpoint Logging Functions");

        Assert.Equal("*", bplog.Version);
        Assert.False(bplog.VersionResolved);
        Assert.Contains("neueste installierte Version", Assert.Single(bplog.Gaps).Message);
    }

    [Fact]
    public void Company_system_is_no_vendor()
    {
        var io = Read(Robin).Software.Single(s => s.Name == "IoStandard");

        Assert.Null(io.Vendor);
        Assert.Equal("System", io.SourceProperties["library:company"]);
        Assert.Contains(io.Gaps, g => g.Code == GapCatalog.SupplierUnknown && g.Message.Contains("„System“"));
    }

    [Fact]
    public void Library_project_gives_a_station_without_devices()
    {
        var station = Read(Osc);

        Assert.Empty(station.Devices);
        Assert.Equal(11, station.Software.Count);
        Assert.Equal(2, station.Software.Count(s => s.Version == "*" && !s.VersionResolved));
    }

    [Fact]
    public void Several_controllers_need_plc()
    {
        var ex = Assert.Throws<UsageException>(() => Read(Synthetic));

        Assert.Equal("CODESYS_PLC_REQUIRED", ex.Code);
        Assert.Contains("Verpackung, Handling", ex.Message);
    }

    [Fact]
    public void Unknown_plc_lists_the_devices_found()
    {
        var ex = Assert.Throws<UsageException>(() => Read(Synthetic, plc: "Presse"));

        Assert.Equal("CODESYS_PLC_NOT_FOUND", ex.Code);
        Assert.Contains("Gefunden: Verpackung, Handling.", ex.Message);
    }

    [Fact]
    public void Plc_selects_the_controller_its_sub_devices_and_libraries()
    {
        var station = Read(Synthetic, plc: "Verpackung");

        Assert.Equal(["Verpackung", "Feldbus_Koppler"], station.Devices.Select(d => d.DeviceName));
        Assert.Equal([true, false], station.Devices.Select(d => d.IsController));
        Assert.Equal("Feldbus_Koppler", station.Devices[1].Position);
        // POU pool + both applications, Util once although both applications reference it.
        Assert.Equal(
            ["CAA Types Extern", "Comquent Diagnose", "IoStandard", "Kaputt", "Standard", "Util"],
            station.Software.Select(s => s.Name));
    }

    [Fact]
    public void Other_controller_gets_only_its_own_libraries()
    {
        var station = Read(Synthetic, plc: "Handling", station: "Handling");

        Assert.Equal(["CAA Types Extern", "Util"], station.Software.Select(s => s.Name));
        Assert.Equal("3.5.18.0", station.Software.Single(s => s.Name == "Util").Version);
    }

    [Fact]
    public void Reference_off_pattern_is_kept_with_a_gap()
    {
        var kaputt = Read(Synthetic, plc: "Verpackung").Software.Single(s => s.Name == "Kaputt");

        Assert.Null(kaputt.Version);
        Assert.Null(kaputt.Vendor);
        Assert.Contains("Titel, Version (Hersteller)", Assert.Single(kaputt.Gaps).Message);
    }

    [Fact]
    public void Missing_file_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => Read("codesys/v35/gibt-es-nicht.xml"));

        Assert.Equal("SOURCE_NOT_FOUND", ex.Code);
    }

    [Fact]
    public void Unique_device_description_gives_name_and_vendor()
    {
        var mux = ReadWithDescriptions().Devices.Single(d => d.DeviceName == "I2C_Mux");

        Assert.Equal("TCA9548", mux.TypeName);
        Assert.Equal("Stefan Dreyer", mux.Vendor!.Name);
        Assert.Equal(ValueOrigin.DeviceDescription, mux.VendorOrigin);
        Assert.Null(mux.OrderNumber);
        Assert.Equal("codesys/devdesc/TCA9548.devdesc.xml", mux.VendorKeys["devdescFile"]);
        Assert.Equal("44f0cccba60ddb56bd5d1623ff903411b44f41aa9bfa03cf419acfbef0345c8f", mux.VendorKeys["devdescSha256"]);
        Assert.Equal([GapCatalog.FirmwareNotInProject, GapCatalog.OrderNumberNotInProject], mux.Gaps.Select(g => g.Code));
        Assert.Contains("TCA9548.devdesc.xml", mux.Gaps[1].Message);
    }

    [Fact]
    public void Ambiguous_device_description_is_not_used()
    {
        var expander = ReadWithDescriptions().Devices.Single(d => d.DeviceName == "Expander");

        Assert.Equal("Gerät 500/ffff 0007", expander.TypeName);
        Assert.Null(expander.Vendor);
        Assert.False(expander.VendorKeys.ContainsKey("devdescFile"));
        var gap = expander.Gaps.Single(g => g.Code == GapCatalog.DevdescNotFound);
        Assert.Contains("PCF8575IN.devdesc.xml", gap.Message);
        Assert.Contains("SSD1306.devdesc.xml", gap.Message);
        Assert.Contains(expander.Gaps, g => g.Code == GapCatalog.SupplierUnknown);
    }

    [Theory]
    [InlineData("Lichtsensor")] // description exists only in another version
    [InlineData("Steuerung")]
    public void Missing_device_description_is_a_gap(string device)
    {
        var d = ReadWithDescriptions().Devices.Single(x => x.DeviceName == device);

        Assert.Null(d.Vendor);
        Assert.Equal(
            [GapCatalog.FirmwareNotInProject, GapCatalog.DevdescNotFound, GapCatalog.OrderNumberNotInProject, GapCatalog.SupplierUnknown],
            d.Gaps.Select(g => g.Code));
    }

    [Fact]
    public void Without_configured_descriptions_there_is_no_devdesc_gap()
    {
        Assert.DoesNotContain(Read(WithDevdesc).Devices.SelectMany(d => d.Gaps), g => g.Code == GapCatalog.DevdescNotFound);
    }

    [Fact]
    public async Task Sbom_with_device_descriptions_matches_the_golden_file_and_passes()
    {
        var bytes = Sbom(ReadWithDescriptions());

        Golden.AssertMatches("codesys-v35-devdesc.cdx.json", bytes);
        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(bytes));
        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Contains("\"value\": \"devdesc\"", Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [InlineData(Robin, null, "codesys-v35-robin.cdx.json")]
    [InlineData(Synthetic, "Verpackung", "codesys-v35-zwei-steuerungen.cdx.json")]
    public void Sbom_matches_the_golden_file(string fixture, string? plc, string golden)
    {
        Golden.AssertMatches(golden, Sbom(Read(fixture, plc)));
    }

    [Theory]
    [InlineData(Robin, null)]
    [InlineData(Osc, null)]
    [InlineData(Ooip, null)]
    [InlineData(Synthetic, "Verpackung")]
    [InlineData(Synthetic, "Handling")]
    public async Task Sbom_passes_schema_and_tr_rules_without_errors(string fixture, string? plc)
    {
        var bytes = Sbom(Read(fixture, plc));

        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(bytes));

        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Equal(bytes, Sbom(Read(fixture, plc)));
    }

    [Fact]
    public void Sbom_writes_codesys_properties()
    {
        var json = Encoding.UTF8.GetString(Sbom(Read(Robin)));

        Assert.Contains("\"name\": \"plcsbom:adapter:source\",\n          \"value\": \"v35\"", json);
        Assert.Contains("\"name\": \"plcsbom:firmware:origin\",\n          \"value\": \"unknown\"", json);
        Assert.Contains("\"name\": \"plcsbom:vendorKey:DeviceDescriptionVersion\"", json);
        Assert.Contains("\"name\": \"plcsbom:library:resolution\"", json);
        Assert.DoesNotContain("\"version\": \"3.5.15.0\"", json); // device description version never as firmware version
    }
}
