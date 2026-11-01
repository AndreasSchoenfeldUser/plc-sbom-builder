using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.Gsdml;
using PlcSbom.Core.Adapters.TiaAml;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Adapters.Gsdml;
using PlcSbom.Tests.Samples;
using static PlcSbom.Tests.Adapters.TiaAml.AmlReaderTests;

namespace PlcSbom.Tests.Adapters.TiaAml;

public sealed class TiaAmlAdapterTests
{
    private static TiaAmlStationOptions Station(string name, string? plc = null) => new() { StationName = name, Plc = plc };

    private static TiaAmlResult Read(string fixture, params TiaAmlStationOptions[] stations) =>
        Read(fixture, new TiaAmlOptions { SourcePath = "export/" + Path.GetFileName(fixture), Stations = stations });

    private static TiaAmlResult Read(string fixture, TiaAmlOptions options) => TiaAmlAdapter.Read(Fixtures.PathOf(fixture), options);

    /// <summary>The V17 fixture with one text replaced, for writer and type identifier cases.</summary>
    private static TiaAmlResult ReadChanged(string oldText, string newText, params TiaAmlStationOptions[] stations) =>
        ReadChanged(oldText, newText, new TiaAmlOptions { SourcePath = "test.aml", Stations = stations.Length == 0 ? [Station("PLC_1")] : stations });

    private static TiaAmlResult ReadChanged(string oldText, string newText, TiaAmlOptions options)
    {
        var xml = Encoding.UTF8.GetString(Fixtures.Read(V17));
        Assert.Contains(oldText, xml);
        var project = AmlReader.Read(Encoding.UTF8.GetBytes(xml.Replace(oldText, newText, StringComparison.Ordinal)), "test.aml");
        return TiaAmlAdapter.ToStations(project, "0", options);
    }

    /// <summary>The V17 fixture as one station with the GSDML directory fixtures/gsdml.</summary>
    private static TiaAmlOptions WithGsdml(GsdmlIndex? index = null) => new()
    {
        SourcePath = "export/plc-commissioning-sample_device.aml",
        Stations = [Station("PLC_1")],
        Gsdml = index ?? GsdmlReaderTests.FixtureIndex(),
    };

    private static TiaAmlResult TwoStations(Func<TiaAmlStationOptions, TiaAmlStationOptions>? plc1 = null) =>
        Read(Synthetic, (plc1 ?? (s => s))(Station("Linie 1", "PLC_1")), Station("Linie 2", "PLC_2"));

    private static Machine Machine(TiaAmlResult result) => new()
    {
        Name = "Maschine XY",
        Release = "R2026.10",
        Commit = HandModel.Commit,
        Supplier = new Party("Musterbau GmbH", Email: "sbom@musterbau.example"),
        Licensing = new Licensing("LicenseRef-musterbau-proprietary", Effective: "LicenseRef-musterbau-proprietary"),
        Stations = result.Stations,
        UnassignedDevices = result.UnassignedDevices,
    };

    /// <summary>Machine around the stations of one file, as build (M9) will assemble it; machine data from the hand model.</summary>
    private static byte[] Sbom(TiaAmlResult result) =>
        CycloneDxWriter.Write(GapRules.Apply(Machine(result)), new WriterOptions { Timestamp = HandModel.Timestamp, ToolVersion = "0.1.0" });

    private static IEnumerable<string> Names(IEnumerable<Device> devices) => devices.Select(d => d.DeviceName!);

    // ---- File level --------------------------------------------------------------------------------

    [Fact]
    public void Station_carries_tia_version_export_time_and_hash()
    {
        var station = Assert.Single(Read(V17, Station("PLC_1")).Stations);

        Assert.Equal(AdapterIds.TiaAml, station.AdapterId);
        Assert.Equal("export/plc-commissioning-sample_device.aml", station.SourcePath);
        Assert.Equal("0fade7c6844270656da6718e515d5f9fcbaa056a72891405bf1ae3c4c73bf1d6", station.SourceSha256);
        Assert.Equal(new ToolInfo("TIA Portal", "V17", "1700.0.4302.1", "Siemens AG"), station.EngineeringTool);
        Assert.Equal(new DateTimeOffset(2024, 12, 28, 20, 29, 17, TimeSpan.Zero).AddTicks(9434236), station.SourceExportedAt);
    }

    [Fact]
    public void Writer_other_than_tia_portal_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => ReadChanged("Totally Integrated Automation Portal", "TIA Selection Tool"));

        Assert.Equal("NOT_TIA_AML", ex.Code);
        Assert.Contains("TIA Selection Tool", ex.Message);
    }

    [Fact]
    public void Unknown_writer_version_is_a_warning_and_kept_as_written()
    {
        var result = ReadChanged("<WriterVersion>1700</WriterVersion>", "<WriterVersion>2200</WriterVersion>");

        Assert.Equal("2200", result.Stations[0].EngineeringTool!.Version);
        Assert.Equal("TIA_AML_WRITER_VERSION_UNKNOWN", Assert.Single(result.Warnings).Code);
    }

    [Fact]
    public void Unknown_type_identifier_prefix_is_a_warning_and_no_component()
    {
        var result = ReadChanged("OrderNumber:6ES7 214-1AG40-0XB0", "Catalog:6ES7 214-1AG40-0XB0");

        Assert.DoesNotContain(result.Stations[0].Devices, d => d.DeviceName == "PLC_1");
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("TIA_AML_TYPE_IDENTIFIER_UNKNOWN", warning.Code);
        Assert.Contains("Catalog:6ES7 214-1AG40-0XB0", warning.Message);
    }

    [Theory]
    [InlineData("OrderNumber:6ES7 511-1AK02-0AB0", "OrderNumber:", "6ES7 511-1AK02-0AB0", null, null, null, null)]
    [InlineData("OrderNumber:6ES7 511-1AK02-0AB0/V2.8", "OrderNumber:", "6ES7 511-1AK02-0AB0", "V2.8", null, null, null)]
    [InlineData("OrderNumber:6ES7 590-1***0-0AA0//Zusatz", "OrderNumber:", "6ES7 590-1***0-0AA0", null, null, null, null)]
    [InlineData("System:Rack.S71500", "System:", null, null, null, null, null)]
    [InlineData("GSD:GSDML-V2.41-LEUZE-BCL248I-20211213.XML/DAP/DAP", "GSD:", null, null, "GSDML-V2.41-LEUZE-BCL248I-20211213.XML", "DAP", "DAP")]
    [InlineData("GSD:GSDML-V2.41-LEUZE-BCL248I-20211213.XML/M/ID_MOD_Activation", "GSD:", null, null, "GSDML-V2.41-LEUZE-BCL248I-20211213.XML", "M", "ID_MOD_Activation")]
    [InlineData("GSD:GSDML-V2.41-LEUZE-BCL248I-20211213.XML/D", "GSD:", null, null, "GSDML-V2.41-LEUZE-BCL248I-20211213.XML", "D", null)]
    [InlineData("Catalog:X", "Catalog:", null, null, null, null, null)]
    public void Type_identifier_is_split_by_prefix(string text, string prefix, string? orderNumber, string? catalog, string? gsdml, string? kind, string? id)
    {
        Assert.Equal(new TiaAmlAdapter.TypeId(prefix, orderNumber, catalog, gsdml, kind, id), TiaAmlAdapter.ParseTypeIdentifier(text));
    }

    // ---- Devices and firmware (CLAUDE.md 5.4.3) -----------------------------------------------------

    [Fact]
    public void Cpu_is_the_controller_with_projected_firmware()
    {
        var cpu = Read(V17, Station("PLC_1")).Stations[0].Devices.Single(d => d.DeviceName == "PLC_1");

        Assert.True(cpu.IsController);
        Assert.Equal(("CPU 1214C DC/DC/DC", "6ES7 214-1AG40-0XB0", "V4.5", FirmwareOrigin.Projected), (cpu.TypeName, cpu.OrderNumber, cpu.Firmware, cpu.FirmwareOrigin));
        Assert.Equal(("CPU", "S7-1200 station_1/0/1"), (cpu.Classification, cpu.Position));
        Assert.Equal("Siemens AG", cpu.Vendor!.Name);
        Assert.Equal(ValueOrigin.Assumed, cpu.VendorOrigin);
        Assert.False(cpu.IsPassive);
        Assert.Empty(cpu.Gaps);
    }

    [Fact]
    public void Firmware_v0_0_and_racks_are_passive()
    {
        var devices = TwoStations().Stations[0].Devices;

        var ba = devices.Single(d => d.DeviceName == "BA 2xRJ45");
        Assert.True(ba.IsPassive);
        Assert.Null(ba.Firmware);
        var rail = devices.First(d => d.DeviceName == "Rail_0");
        Assert.True(rail.IsPassive);
        Assert.Equal("6ES7 590-1***0-0AA0", rail.OrderNumber);
        Assert.True(rail.OrderNumberIsPattern);
        Assert.DoesNotContain(devices, d => d.DeviceName == "Rack_0"); // System:Rack.ET200SP is structure
    }

    [Fact]
    public void Device_without_firmware_attribute_is_a_warning()
    {
        var pm = TwoStations().Stations[0].Devices.Single(d => d.OrderNumber == "6EP1332-4BA00");

        Assert.False(pm.IsPassive);
        Assert.Null(pm.Firmware);
        Assert.Equal(FirmwareOrigin.NotInProject, pm.FirmwareOrigin);
        Assert.Equal([GapCatalog.FirmwareAttributeMissing, GapCatalog.FirmwareNotInProject], pm.Gaps.Select(g => g.Code));
        Assert.Contains("passiveOrderNumbers", pm.Gaps[0].Message);
        Assert.Equal(Severity.Warning, GapCatalog.Find(GapCatalog.FirmwareAttributeMissing)!.Severity);
    }

    [Fact]
    public void Passive_order_numbers_declare_devices_without_firmware_attribute()
    {
        var result = Read(Synthetic, new TiaAmlOptions
        {
            SourcePath = "export/Linie1.aml",
            Stations = [Station("Linie 1", "PLC_1"), Station("Linie 2", "PLC_2")],
            PassiveOrderNumbers = ["6EP1*", "6ES7 521*"],
        });

        var pm = result.Stations[0].Devices.Single(d => d.OrderNumber == "6EP1332-4BA00");
        Assert.True(pm.IsPassive);
        Assert.Equal("declared", pm.SourceProperties["device:passive"]);
        Assert.Empty(pm.Gaps);
        // Only devices without FirmwareVersion: the DI module keeps its firmware V2.2.
        Assert.False(result.Stations[0].Devices.Single(d => d.OrderNumber == "6ES7 521-1BH00-0AB0").IsPassive);
    }

    [Fact]
    public void Vendor_overrides_win_over_the_assumption()
    {
        var result = Read(Synthetic, new TiaAmlOptions
        {
            SourcePath = "export/Linie1.aml",
            Stations = [Station("Linie 1", "PLC_1"), Station("Linie 2", "PLC_2")],
            VendorOverrides = [new("6SL3*", "Siemens AG Digital Industries"), new("6SL*", "nie erreicht")],
        });

        var drive = result.Stations[1].Devices.Single(d => d.DeviceName == "Pump_1");
        Assert.Equal("Siemens AG Digital Industries", drive.Vendor!.Name);
        Assert.Equal(ValueOrigin.Declared, drive.VendorOrigin);
        Assert.Equal("4.7.10", drive.Firmware); // unchanged, without "V"
    }

    [Fact]
    public void Catalogue_version_is_a_vendor_key_and_a_mismatch_warns()
    {
        var result = TwoStations();

        var di = result.Stations[0].Devices.Single(d => d.DeviceName == "DI 16x24VDC HF_1");
        Assert.Equal("6ES7 521-1BH00-0AB0", di.OrderNumber);
        Assert.Equal("V2.1", di.VendorKeys["catalogVersion"]);
        Assert.Equal("V2.2", di.Firmware);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("TIA_AML_CATALOG_VERSION_DIFFERS", warning.Code);
        Assert.Contains("V2.1", warning.Message);
    }

    [Fact]
    public void Gsd_head_module_without_gsdml_directory_has_no_order_number_vendor_or_firmware()
    {
        var gsd = Read(V17, Station("PLC_1")).Stations[0].Devices.Single(d => d.DeviceName == "BCL248i");

        Assert.Equal(("BCL248i", "HeadModule", "GSD device_1/0/0"), (gsd.TypeName, gsd.Classification, gsd.Position));
        Assert.Null(gsd.OrderNumber);
        Assert.Null(gsd.Vendor);
        Assert.Null(gsd.Firmware);
        Assert.False(gsd.IsPassive);
        Assert.Equal(
            new Dictionary<string, string> { ["GSDML"] = "GSDML-V2.41-LEUZE-BCL248I-20211213.XML", ["GsdId"] = "DAP", ["GsdType"] = "DAP" },
            gsd.VendorKeys);
        Assert.Equal([GapCatalog.GsdmlNotFound, GapCatalog.OrderNumberNotInProject, GapCatalog.SupplierUnknown, GapCatalog.FirmwareNotInProject], gsd.Gaps.Select(g => g.Code));
        Assert.Contains("kein Verzeichnis", gsd.Gaps[0].Message);
        Assert.Contains("SoftwareRelease", gsd.Gaps[3].Message);
    }

    [Fact]
    public void Gsd_modules_are_passive()
    {
        var modules = Read(V17, Station("PLC_1")).Stations[0].Devices.Where(d => d.VendorKeys.GetValueOrDefault("GsdType") == "M").ToList();

        Assert.Equal(["[M10] Activation_1", "[M11] Reading gate control_1"], Names(modules));
        Assert.All(modules, m => Assert.True(m.IsPassive));
        Assert.All(modules, m => Assert.Empty(m.Gaps));
    }

    // ---- GSDML (CLAUDE.md 5.4.5) ----------------------------------------------------------------------

    [Fact]
    public void Gsdml_gives_vendor_and_order_number_of_the_head_module()
    {
        var gsd = Read(V17, WithGsdml()).Stations[0].Devices.Single(d => d.DeviceName == "BCL248i");

        Assert.Equal("Leuze electronic GmbH + Co. KG", gsd.Vendor!.Name);
        Assert.Equal(ValueOrigin.Gsdml, gsd.VendorOrigin);
        Assert.Equal("501xxxxx", gsd.OrderNumber);
        Assert.True(gsd.OrderNumberIsPattern); // one GSDML for several articles
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["DeviceId"] = "0x0015",
                ["GSDML"] = "GSDML-V2.41-LEUZE-BCL248I-20211213.XML",
                ["GsdId"] = "DAP",
                ["GsdType"] = "DAP",
                ["VendorId"] = "0x0152",
                ["gsdmlFile"] = "gsdml/GSDML-V2.41-LEUZE-BCL248i-20211213.xml",
                ["gsdmlSha256"] = "ca1b97e3068ce1663ebba66422d9fbf77d314fc8ff3dfe589ad8c0bf6156eaf7",
            },
            gsd.VendorKeys);
        Assert.Empty(Read(V17, WithGsdml()).Warnings);
    }

    [Fact]
    public void Software_release_of_the_gsdml_is_not_the_firmware()
    {
        var gsd = Read(V17, WithGsdml()).Stations[0].Devices.Single(d => d.DeviceName == "BCL248i");

        Assert.Null(gsd.Firmware);
        Assert.Equal(FirmwareOrigin.NotInProject, gsd.FirmwareOrigin);
        Assert.Equal(new Dictionary<string, string> { ["gsdml:hardwareRelease"] = "3", ["gsdml:softwareRelease"] = "V1.*.*" }, gsd.SourceProperties);
        Assert.Equal([GapCatalog.FirmwareNotInProject], gsd.Gaps.Select(g => g.Code));
    }

    [Fact]
    public void Gsd_modules_get_the_vendor_and_stay_passive()
    {
        var modules = Read(V17, WithGsdml()).Stations[0].Devices.Where(d => d.VendorKeys.GetValueOrDefault("GsdType") == "M").ToList();

        Assert.Equal(2, modules.Count);
        Assert.All(modules, m => Assert.True(m.IsPassive));
        Assert.All(modules, m => Assert.Equal(("Leuze electronic GmbH + Co. KG", ValueOrigin.Gsdml), (m.Vendor!.Name, m.VendorOrigin)));
        Assert.All(modules, m => Assert.Null(m.OrderNumber)); // the file names none for its modules
    }

    [Fact]
    public void Gsdml_not_in_the_directories_gives_the_gaps()
    {
        using var empty = new GsdmlReaderTests.TempDir();

        var gsd = Read(V17, WithGsdml(GsdmlIndex.Load([(empty.Path, "gsdml")]))).Stations[0].Devices.Single(d => d.DeviceName == "BCL248i");

        Assert.Null(gsd.Vendor);
        Assert.Equal([GapCatalog.GsdmlNotFound, GapCatalog.OrderNumberNotInProject, GapCatalog.SupplierUnknown, GapCatalog.FirmwareNotInProject], gsd.Gaps.Select(g => g.Code));
        Assert.Equal("Die GSDML-Datei GSDML-V2.41-LEUZE-BCL248I-20211213.XML liegt in keinem der Verzeichnisse aus „gsdml“.", gsd.Gaps[0].Message);
        Assert.Equal(Severity.Warning, GapCatalog.Find(GapCatalog.GsdmlNotFound)!.Severity);
    }

    [Fact]
    public void Ambiguous_gsdml_gives_gsdml_not_found_with_the_candidates()
    {
        using var a = new GsdmlReaderTests.TempDir();
        using var b = new GsdmlReaderTests.TempDir();
        a.Write(GsdmlReaderTests.LeuzeInAml, Fixtures.Read(GsdmlReaderTests.Leuze));
        b.Write(GsdmlReaderTests.LeuzeInAml, [.. Fixtures.Read(GsdmlReaderTests.Leuze), (byte)'\n']);

        var result = Read(V17, WithGsdml(GsdmlIndex.Load([(a.Path, "a"), (b.Path, "b")])));

        var gap = result.Stations[0].Devices.Single(d => d.DeviceName == "BCL248i").Gaps[0];
        Assert.Equal(GapCatalog.GsdmlNotFound, gap.Code);
        Assert.Contains("mehrfach mit verschiedenem Inhalt", gap.Message);
        Assert.Contains("a/GSDML-V2.41-LEUZE-BCL248I-20211213.XML, b/", gap.Message);
    }

    [Fact]
    public void Access_point_missing_in_the_gsdml_is_a_warning_and_leaves_the_order_number_open()
    {
        var options = WithGsdml() with { SourcePath = "test.aml" };
        var result = ReadChanged("XML/DAP/DAP<", "XML/DAP/DAP_X<", options);

        var gsd = result.Stations[0].Devices.Single(d => d.DeviceName == "BCL248i");
        Assert.Equal("Leuze electronic GmbH + Co. KG", gsd.Vendor!.Name); // the file belongs to the vendor
        Assert.Null(gsd.OrderNumber);
        Assert.Equal([GapCatalog.OrderNumberNotInProject, GapCatalog.FirmwareNotInProject], gsd.Gaps.Select(g => g.Code));
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("TIA_AML_GSDML_ITEM_NOT_FOUND", warning.Code);
        Assert.Contains("DAP_X", warning.Message);
    }

    [Fact]
    public void Vendor_overrides_win_over_the_gsdml()
    {
        var gsd = Read(V17, WithGsdml() with { VendorOverrides = [new("501*", "Leuze electronic GmbH + Co. KG, Owen")] })
            .Stations[0].Devices.Single(d => d.DeviceName == "BCL248i");

        Assert.Equal(("Leuze electronic GmbH + Co. KG, Owen", ValueOrigin.Declared), (gsd.Vendor!.Name, gsd.VendorOrigin));
    }

    // ---- Stations (CLAUDE.md 5.4.4) -----------------------------------------------------------------

    [Fact]
    public void Without_plc_the_whole_file_is_the_station()
    {
        var result = Read(V17, Station("PLC_1"));

        var station = Assert.Single(result.Stations);
        Assert.Equal(["PLC_1", "BCL248i", "[M10] Activation_1", "[M11] Reading gate control_1"], Names(station.Devices).OrderBy(n => n == "PLC_1" ? 0 : 1));
        Assert.Empty(result.UnassignedDevices);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void With_plc_devices_off_the_io_system_are_unassigned()
    {
        var result = Read(V17, Station("PLC_1", "PLC_1"));

        Assert.Equal(["PLC_1"], Names(result.Stations[0].Devices));
        var gsd = result.UnassignedDevices.Single(d => d.DeviceName == "BCL248i");
        Assert.Contains(gsd.Gaps, g => g.Code == GapCatalog.DeviceUnassigned && g.Message.Contains("GSD device_1"));
        Assert.Equal("tia-aml", gsd.SourceProperties["adapter"]);
        Assert.Equal("export/plc-commissioning-sample_device.aml", gsd.SourceProperties["source"]);
        Assert.All(result.UnassignedDevices.Where(d => d.IsPassive), d => Assert.DoesNotContain(d.Gaps, g => g.Code == GapCatalog.DeviceUnassigned));
    }

    [Fact]
    public void Two_cpus_give_two_stations_split_by_io_system()
    {
        var result = TwoStations();

        Assert.Equal(
            ["PM 70W 120/230VAC", "PLC_1", "DI 16x24VDC HF_1", "IO_Device_1", "AI 4xU/I 2-wire ST_1", "Scanner_1"],
            Names(result.Stations[0].Devices.Where(d => !d.IsPassive)));
        Assert.Equal(["PLC_2", "IO_Device_2", "Pump_1"], Names(result.Stations[1].Devices.Where(d => !d.IsPassive)));
        Assert.Equal([true, false], result.Stations.Select(s => s.Devices.Count(d => d.IsController) == 1 && s.Devices.Single(d => d.IsController).DeviceName == "PLC_1"));
        Assert.Equal(["IO_Test", "Pump_2"], Names(result.UnassignedDevices));
        Assert.Empty(result.IgnoredDevices);
    }

    [Fact]
    public void Devices_pattern_adds_a_device_without_io_system()
    {
        var result = TwoStations(s => s with { Devices = ["SINAMICS G_2"] });

        Assert.Contains("Pump_2", Names(result.Stations[0].Devices));
        Assert.Equal(["IO_Test"], Names(result.UnassignedDevices));
    }

    [Fact]
    public void Devices_pattern_matches_item_names_too()
    {
        // Pump_1 (SINAMICS G_1) already belongs to Linie 2 over its IO system, Pump_2 to no station.
        var ex = Assert.Throws<UsageException>(() => TwoStations(s => s with { Devices = ["Pump_?"] }));

        Assert.Equal("TIA_AML_DEVICE_IN_TWO_STATIONS", ex.Code);
        Assert.Contains("SINAMICS G_1", ex.Message);
    }

    [Fact]
    public void Ignored_devices_are_listed_separately()
    {
        var result = TwoStations(s => s with { IgnoreDevices = ["Testaufbau_*"] });

        Assert.Equal(["IO_Test"], Names(result.IgnoredDevices));
        Assert.Equal(["Pump_2"], Names(result.UnassignedDevices));
    }

    [Fact]
    public void Unused_patterns_are_warnings()
    {
        var result = TwoStations(s => s with { Devices = ["HMI_1"], IgnoreDevices = ["Tippfehler*"] });

        Assert.Equal(["TIA_AML_PATTERN_UNUSED", "TIA_AML_PATTERN_UNUSED"], result.Warnings.Where(w => w.Code != "TIA_AML_CATALOG_VERSION_DIFFERS").Select(w => w.Code));
    }

    [Fact]
    public void Device_claimed_by_two_stations_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => TwoStations(s => s with { Devices = ["SINAMICS G_1"] }));

        Assert.Equal("TIA_AML_DEVICE_IN_TWO_STATIONS", ex.Code);
        Assert.Contains("SINAMICS G_1", ex.Message);
    }

    [Fact]
    public void Same_cpu_for_two_stations_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => Read(Synthetic, Station("A", "PLC_1"), Station("B", "PLC_1")));

        Assert.Equal("TIA_AML_DEVICE_IN_TWO_STATIONS", ex.Code);
    }

    [Fact]
    public void Unknown_plc_ends_with_exit_2_and_lists_the_cpus()
    {
        var ex = Assert.Throws<UsageException>(() => Read(Synthetic, Station("Linie 1", "PLC_9")));

        Assert.Equal("TIA_AML_PLC_NOT_FOUND", ex.Code);
        Assert.Contains("PLC_1, PLC_2", ex.Message);
    }

    [Fact]
    public void Several_cpus_without_plc_end_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => Read(Synthetic, Station("Linie 1")));

        Assert.Equal("TIA_AML_PLC_REQUIRED", ex.Code);
        Assert.Contains("PLC_1, PLC_2", ex.Message);
    }

    [Fact]
    public void Two_stations_on_one_file_need_plc_each()
    {
        var ex = Assert.Throws<UsageException>(() => Read(Synthetic, Station("Linie 1", "PLC_1"), Station("Linie 2")));

        Assert.Equal("TIA_AML_PLC_REQUIRED", ex.Code);
    }

    [Fact]
    public void Missing_file_ends_with_exit_2()
    {
        Assert.Equal("SOURCE_NOT_FOUND", Assert.Throws<UsageException>(() => Read("tia-aml/gibt-es-nicht.aml", Station("X"))).Code);
    }

    // ---- Libraries and program ------------------------------------------------------------------------

    [Fact]
    public void Station_without_declared_libraries_carries_both_gaps()
    {
        var station = Read(V17, Station("PLC_1")).Stations[0];

        Assert.Empty(station.Software);
        Assert.Equal([GapCatalog.LibrariesNotRead, GapCatalog.ProgramNotRead], station.Gaps.Select(g => g.Code));
    }

    [Fact]
    public void Declared_libraries_replace_libraries_not_read()
    {
        var station = Read(V17, Station("PLC_1") with { Libraries = [new("LGF", "Siemens AG", "7.0.1"), new("Comquent_Basis")] }).Stations[0];

        Assert.Equal([GapCatalog.ProgramNotRead], station.Gaps.Select(g => g.Code));
        var lgf = station.Software.Single(s => s.Name == "LGF");
        Assert.Equal((SoftwareKind.Library, "7.0.1", true, ValueOrigin.Declared, ValueOrigin.Declared), (lgf.Kind, lgf.Version, lgf.VersionResolved, lgf.Origin, lgf.VendorOrigin));
        var own = station.Software.Single(s => s.Name == "Comquent_Basis");
        Assert.Null(own.Vendor);
        Assert.False(own.VersionResolved);
    }

    // ---- SBOM -----------------------------------------------------------------------------------------

    public static TheoryData<string, string, string?> GoldenCases => new()
    {
        { V17, "tia-aml-v17-ganze-datei.cdx.json", null },
        { V17, "tia-aml-v17-plc.cdx.json", "PLC_1" },
    };

    [Fact]
    public void Sbom_with_gsdml_matches_the_golden_file()
    {
        Golden.AssertMatches("tia-aml-v17-gsdml.cdx.json", Sbom(Read(V17, WithGsdml())));
    }

    [Fact]
    public async Task Sbom_with_gsdml_passes_schema_and_tr_rules_without_errors()
    {
        var json = Encoding.UTF8.GetString(Sbom(Read(V17, WithGsdml())));

        var report = await SbomValidator.ValidateAsync(json);

        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Contains("\"name\": \"plcsbom:gsdml:softwareRelease\",\n          \"value\": \"V1.*.*\"", json);
        Assert.Contains("\"name\": \"plcsbom:device:vendor:origin\",\n          \"value\": \"gsdml\"", json);
        Assert.Contains("\"name\": \"plcsbom:device:orderNumber:pattern\",\n          \"value\": \"true\"", json);
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void Sbom_matches_the_golden_file(string fixture, string golden, string? plc)
    {
        Golden.AssertMatches(golden, Sbom(Read(fixture, Station("PLC_1", plc))));
    }

    [Fact]
    public void Sbom_of_two_stations_matches_the_golden_file()
    {
        Golden.AssertMatches("tia-aml-zwei-cpus.cdx.json", Sbom(TwoStations(s => s with
        {
            Devices = ["SINAMICS G_2"],
            IgnoreDevices = ["Testaufbau_*"],
            Libraries = [new("LGF", "Siemens AG", "7.0.1")],
        })));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("PLC_1")]
    public async Task Sbom_passes_schema_and_tr_rules_without_errors(string? plc)
    {
        var bytes = Sbom(Read(V17, Station("PLC_1", plc)));

        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(bytes));

        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Equal(bytes, Sbom(Read(V17, Station("PLC_1", plc))));
    }

    [Fact]
    public async Task Sbom_of_two_stations_passes_without_errors()
    {
        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(Sbom(TwoStations())));

        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Contains(report.Findings, f => f.Code == "FIRMWARE_ATTRIBUTE_MISSING" && f.Severity == Severity.Warning);
    }

    [Theory]
    [InlineData("192.0.2.")]
    [InlineData("255.255.255.0")]
    [InlineData("IpProtocolSelection")]
    public void Sbom_contains_no_network_data(string text)
    {
        Assert.Contains(text, Encoding.UTF8.GetString(Fixtures.Read(Synthetic)));
        Assert.DoesNotContain(text, Encoding.UTF8.GetString(Sbom(TwoStations())));
    }

    [Fact]
    public void Sbom_writes_tia_properties()
    {
        var json = Encoding.UTF8.GetString(Sbom(TwoStations()));

        Assert.Contains("\"bom-ref\": \"linie-1-fw\"", json);
        Assert.Contains("\"name\": \"plcsbom:source:exportedAt\",\n          \"value\": \"2026-10-22T08:15:00Z\"", json);
        Assert.Contains("\"name\": \"plcsbom:device:position\",\n          \"value\": \"S71500/ET200MP station_1/0/1\"", json);
        Assert.Contains("\"name\": \"plcsbom:vendorKey:catalogVersion\",\n          \"value\": \"V2.1\"", json);
        Assert.Contains("\"name\": \"plcsbom:gap\",\n          \"value\": \"firmware-attribute-missing\"", json);
        Assert.Contains("\"name\": \"plcsbom:gap\",\n          \"value\": \"libraries-not-read\"", json);
        Assert.DoesNotContain("BA 2xRJ45", json); // passive: hardware appendix of the report, not the SBOM
    }

    // ---- Local fixtures without licence (fixtures/_local) ---------------------------------------------

    [Fact]
    public async Task Local_v18_export_runs_without_errors()
    {
        SkipUnlessLocal(LocalV18);

        var result = Read(LocalV18, Station("PLC_CIP", "PLC_CIP"));

        var station = Assert.Single(result.Stations);
        Assert.Equal("V18", station.EngineeringTool!.Version);
        Assert.Empty(result.UnassignedDevices); // all seven devices hang on the IO system
        Assert.Equal(3, station.Devices.Count(d => d.TypeName == "G120C PN" && d.Firmware == "4.7.10"));
        Assert.Equal(4, station.Devices.Count(d => d.TypeName == "IM 155-6 PN HF" && d.Firmware == "V3.3"));
        Assert.True(station.Devices.Single(d => d.DeviceName == "PLC_CIP").IsController);
        var bytes = Sbom(result);
        Assert.DoesNotContain("192.168.0.", Encoding.UTF8.GetString(bytes));
        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(bytes));
        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Local_v21_export_runs_without_errors()
    {
        SkipUnlessLocal(LocalV21);

        var result = Read(LocalV21, Station("PLC_1"));

        var station = Assert.Single(result.Stations);
        Assert.Equal(new ToolInfo("TIA Portal", "V21", "2100.0.121.1", "Siemens"), station.EngineeringTool);
        Assert.Contains(station.Devices.Single(d => d.OrderNumber == "6EP1332-4BA00").Gaps, g => g.Code == GapCatalog.FirmwareAttributeMissing);
        Assert.True(station.Devices.Single(d => d.DeviceName == "Profilschiene_0").OrderNumberIsPattern);
        Assert.Equal("V2.8", station.Devices.Single(d => d.IsController).Firmware);
        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(Sbom(result)));
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
    }

    [Theory]
    [InlineData(LocalSelectionTool, "TIA Selection Tool")]
    [InlineData(LocalBeckhoff, "Mahdi")]
    public void Local_exports_from_other_tools_end_with_exit_2(string fixture, string writer)
    {
        SkipUnlessLocal(fixture);

        var ex = Assert.Throws<UsageException>(() => Read(fixture, Station("X")));

        Assert.Equal("NOT_TIA_AML", ex.Code);
        Assert.Contains(writer, ex.Message);
    }
}
