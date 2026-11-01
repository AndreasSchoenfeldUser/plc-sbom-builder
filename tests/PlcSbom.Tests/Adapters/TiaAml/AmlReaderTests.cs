using System.Text;
using System.Text.Json;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.TiaAml;

namespace PlcSbom.Tests.Adapters.TiaAml;

public sealed class AmlReaderTests
{
    internal const string V17 = "tia-aml/plc-commissioning-sample_device.aml";
    internal const string Synthetic = "tia-aml/zwei-cpus-synthetisch.aml";

    // Without licence, only in fixtures/_local (FIXTURES.md); tests using them skip when they are missing.
    internal const string LocalV18 = "_local/tia-aml/openess_CIP-CIP_cs_HardwareNetwork.aml";
    internal const string LocalV21 = "_local/tia-aml/interoperabilitaet-TIA_Export.aml";
    internal const string LocalSelectionTool = "_local/tia-aml/interoperabilitaet-TST_Export.aml";
    internal const string LocalBeckhoff = "_local/tia-aml/interoperabilitaet-beispiel_export.aml";

    internal static void SkipUnlessLocal(string fixture) =>
        Assert.SkipUnless(File.Exists(Fixtures.PathOf(fixture)), $"Lokale Fixture {fixture} fehlt (fixtures/FIXTURES.md).");

    private static AmlProject Read(string fixture) => AmlReader.Read(Fixtures.Read(fixture), Path.GetFileName(fixture));

    private static AmlProject ReadXml(string xml) => AmlReader.Read(Encoding.UTF8.GetBytes(xml), "test.aml");

    [Fact]
    public void Header_and_project_of_a_v17_export()
    {
        var project = Read(V17);

        Assert.Equal("2.15", project.SchemaVersion);
        Assert.Equal(
            new AmlWriterHeader("Totally Integrated Automation Portal", "Siemens AG", "1700", "1700.0.4302.1", "2024-12-28T20:29:17.9434236Z"),
            project.Writer);
        Assert.Equal("Blank_project_S71200", project.ProjectName);
        Assert.Equal(["GSD device_1", "S7-1200 station_1"], project.Devices.Select(d => d.Name));
        Assert.Empty(project.UnknownRoles);
    }

    [Fact]
    public void Built_in_items_are_not_listed_and_positions_follow_the_rack()
    {
        var station = Read(V17).Devices.Single(d => d.Name == "S7-1200 station_1");

        Assert.Equal("System:Device.S71200", station.TypeIdentifier);
        Assert.Equal(["Rack_0", "PLC_1"], station.Items.Select(i => i.Name)); // DI 14/DQ 10_1, HSC_1 … are BuiltIn
        var cpu = station.Items[1];
        Assert.Equal(
            ("0/1", "OrderNumber:6ES7 214-1AG40-0XB0", "CPU 1214C DC/DC/DC", "V4.5", "CPU"),
            (cpu.Position, cpu.TypeIdentifier, cpu.TypeName, cpu.FirmwareVersion, cpu.DeviceItemType));
    }

    [Fact]
    public void Gsd_device_in_folder_with_modules()
    {
        var gsd = Read(V17).Devices.Single(d => d.Name == "GSD device_1");

        Assert.Equal("Ungrouped devices", gsd.Folder);
        Assert.Equal("GSD:GSDML-V2.41-LEUZE-BCL248I-20211213.XML/D", gsd.TypeIdentifier);
        Assert.Equal(
            [("Rack", "0"), ("BCL248i", "0/0"), ("[M10] Activation_1", "0/1"), ("[M11] Reading gate control_1", "0/2")],
            gsd.Items.Select(i => (i.Name, i.Position)));
        Assert.Equal("HeadModule", gsd.Items[1].DeviceItemType);
        Assert.Null(gsd.Items[1].FirmwareVersion);
        Assert.Empty(gsd.LinkedIoSystemIds); // only linked to the subnet
    }

    [Fact]
    public void Io_system_belongs_to_its_cpu()
    {
        var station = Read(V17).Devices.Single(d => d.Name == "S7-1200 station_1");

        var io = Assert.Single(station.IoSystems);
        Assert.Equal("PROFINET IO-System", io.Name);
        Assert.Equal(station.Items.Single(i => i.Name == "PLC_1").Id, io.CpuItemId);
    }

    [Fact]
    public void Link_to_io_system_connects_the_device_of_the_interface()
    {
        var project = Read(Synthetic);
        var ioPlc1 = project.Devices.Single(d => d.Name == "S71500/ET200MP station_1").IoSystems.Single().Id;
        var ioPlc2 = project.Devices.Single(d => d.Name == "S71500/ET200MP station_2").IoSystems.Single().Id;

        Assert.Equal([ioPlc1], project.Devices.Single(d => d.Name == "ET 200SP station_1").LinkedIoSystemIds); // interface below BA 2xRJ45
        Assert.Equal([ioPlc2], project.Devices.Single(d => d.Name == "SINAMICS G_1").LinkedIoSystemIds);
        Assert.Equal([ioPlc1], project.Devices.Single(d => d.Name == "GSD device_1").LinkedIoSystemIds);
        Assert.Empty(project.Devices.Single(d => d.Name == "SINAMICS G_2").LinkedIoSystemIds);
    }

    [Fact]
    public void Control_characters_are_removed_from_names()
    {
        var drive = Read(Synthetic).Devices.Single(d => d.Name == "SINAMICS G_1");

        Assert.Equal("_0", drive.Items[0].Name); // Name="&#x9;_0" as in the V18 fixture
        Assert.Equal("_0", AmlReader.CleanName("\t_0"));
        Assert.Equal("", AmlReader.CleanName(null));
    }

    [Fact]
    public void Network_data_and_tag_tables_are_not_read()
    {
        var json = JsonSerializer.Serialize(Read(Synthetic));

        Assert.Contains("192.0.2.1", Encoding.UTF8.GetString(Fixtures.Read(Synthetic)));
        Assert.DoesNotContain("192.0.2.", json);
        Assert.DoesNotContain("255.255.255.0", json);
        Assert.DoesNotContain("Default tag table", json);
        Assert.DoesNotContain("\"Id\"", json); // element ids are internal
    }

    [Fact]
    public void Other_root_element_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => ReadXml("<RSLogix5000Content />"));

        Assert.Equal("NOT_AML", ex.Code);
    }

    [Fact]
    public void Dtd_is_refused()
    {
        var ex = Assert.Throws<UsageException>(() => ReadXml("<!DOCTYPE CAEXFile [<!ENTITY x \"y\">]><CAEXFile />"));

        Assert.Equal("XML_INVALID", ex.Code);
    }

    [Fact]
    public void Namespace_and_unknown_roles_do_not_stop_the_reader()
    {
        // CAEX 3.0 has a namespace; the reader works on local names (CLAUDE.md 5.4.1).
        var project = ReadXml("""
            <CAEXFile xmlns="http://www.dke.de/CAEX" SchemaVersion="3.0">
              <InstanceHierarchy Name="H">
                <InternalElement ID="p" Name="Projekt">
                  <InternalElement ID="x" Name="Hmi">
                    <InternalElement ID="d" Name="Station">
                      <InternalElement ID="i" Name="Modul">
                        <Attribute Name="TypeIdentifier"><Value>OrderNumber:6ES7 000-0AA00-0AA0</Value></Attribute>
                        <SupportedRoleClass RefRoleClassPath="AutomationProjectConfigurationRoleClassLib/DeviceItem" />
                      </InternalElement>
                      <SupportedRoleClass RefRoleClassPath="AutomationProjectConfigurationRoleClassLib/Device" />
                    </InternalElement>
                    <SupportedRoleClass RefRoleClassPath="SonstigeRoleClassLib/HmiTarget" />
                  </InternalElement>
                  <SupportedRoleClass RefRoleClassPath="AutomationProjectConfigurationRoleClassLib/AutomationProject" />
                </InternalElement>
              </InstanceHierarchy>
            </CAEXFile>
            """);

        Assert.Equal("Projekt", project.ProjectName);
        Assert.Equal(["HmiTarget"], project.UnknownRoles);
        var item = Assert.Single(Assert.Single(project.Devices).Items);
        Assert.Equal("OrderNumber:6ES7 000-0AA00-0AA0", item.TypeIdentifier);
        Assert.Equal("Modul", item.Position); // no PositionNumber: the name stands in the path
    }

    [Fact]
    public void Local_v18_export_has_seven_devices_on_the_io_system()
    {
        SkipUnlessLocal(LocalV18);

        var project = Read(LocalV18);

        Assert.Equal("1800", project.Writer.Version);
        Assert.Equal(8, project.Devices.Count);
        Assert.Equal(7, project.Devices.Count(d => d.LinkedIoSystemIds.Count == 1));
        Assert.Equal(["_0", "_0", "_0"], project.Devices.Where(d => d.Name.StartsWith("SINAMICS", StringComparison.Ordinal)).Select(d => d.Items[0].Name));
    }
}
