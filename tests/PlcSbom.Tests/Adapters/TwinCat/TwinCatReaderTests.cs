using PlcSbom.Core;
using PlcSbom.Core.Adapters.TwinCat;

namespace PlcSbom.Tests.Adapters.TwinCat;

public sealed class TwinCatReaderTests
{
    internal const string HeatPump = "twincat/aixocat-heatpump/HeatPumpTestBenchControl.tsproj";
    internal const string Spt = "twincat/spt-ethercat-diagnostics/SPT EtherCAT Diagnostics.tsproj";
    internal const string Cognex = "twincat/tcopen-cognex/XAETcoCognexVision.tsproj";
    internal const string TcoCoreProject = "twincat/tcopen-core/XaeTcoCore.tsproj";
    internal const string Synthetic = "twincat/synthetisch/zelle-synthetisch.tsproj";

    private static TwinCatProject Read(string fixture) =>
        TwinCatReader.Read(Fixtures.PathOf(fixture), Fixtures.Read(fixture), Path.GetFileName(fixture));

    [Fact]
    public void Ethercat_tree_with_identity_and_disabled_master()
    {
        var project = Read(HeatPump);

        Assert.Equal("3.1.4022.4", project.TcVersion);
        var device = Assert.Single(project.IoDevices);
        Assert.Equal(("Device 1 (EtherCAT)", "111", true), (device.Name, device.DevType, device.Disabled));
        var coupler = Assert.Single(device.Boxes);
        Assert.Equal(
            ("1", "Coupler (EK1100)", "EtherCAT", "#x00000002", "#x044c2c52", "#x00120000", "EK1100"),
            (coupler.Id, coupler.Name, coupler.Fieldbus, coupler.VendorId, coupler.ProductCode, coupler.RevisionNo, coupler.Desc));
        Assert.Equal(9, coupler.Children.Count);
        Assert.Equal("EL3204-0200 4Ch. Ana. In. PT1000 (RTD)", coupler.Children.Single(b => b.Id == "3").Type);
    }

    [Fact]
    public void Plc_project_with_placeholders_is_read()
    {
        var plc = Assert.Single(Read(HeatPump).PlcProjects);

        Assert.Equal(("HeatPumpTestBenchControl", "HeatPumpTestBenchControl/HeatPumpTestBenchControl.plcproj", "3.1.4022.1"), (plc.Name, plc.File, plc.ProgramVersion));
        Assert.Equal(14, plc.Libraries.Count);
        var visu = plc.Libraries.Single(l => l.Name == "system_visuinputs");
        Assert.Equal(new TwinCatLibrary(TwinCatLibraryKind.Placeholder, "system_visuinputs", "visuinputs, 3.5.6.0 (system)", null, "visuinputs", true), visu);
    }

    [Fact]
    public void Plc_project_via_independent_file_in_config_folder()
    {
        var project = Read(Spt);

        var plc = Assert.Single(project.PlcProjects);
        Assert.Equal(("EtherCatDiagnosticsSample", "EtherCatDiagnosticsSample/EtherCatDiagnosticsSample.plcproj"), (plc.Name, plc.File));
        Assert.Equal(16, plc.Libraries.Count);
        var end = project.IoDevices[0].Boxes[0].Children.Single(b => b.Id == "9");
        Assert.Equal(("EL9011", null), (end.Desc, end.RevisionNo));
    }

    [Fact]
    public void Profinet_boxes_have_no_ethercat_identity()
    {
        var device = Assert.Single(Read(Cognex).IoDevices);

        Assert.Equal("113", device.DevType);
        Assert.All(device.Boxes, b => Assert.Equal(("Profinet", null), (b.Fieldbus, b.VendorId)));
        Assert.Equal(["myveryfirstdataman", "myveryfirstinsight"], device.Boxes.Select(b => b.Name));
    }

    [Fact]
    public void Direct_references_resolutions_and_several_plc_projects()
    {
        var project = Read(TcoCoreProject);

        Assert.Equal(["TcoCore", "TcoCoreTests", "TcoCoreExamples"], project.PlcProjects.Select(p => p.Name));
        var core = project.PlcProjects[0].Libraries;
        Assert.Contains(core, l => l.Kind == TwinCatLibraryKind.Direct && l.Name == "Tc2_EtherCAT,newest,Beckhoff Automation GmbH");
        Assert.Contains(core, l => l.Kind == TwinCatLibraryKind.Placeholder && l.Name == "Tc2_System" && l.Resolution == "Tc2_System, * (Beckhoff Automation GmbH)");
        Assert.Contains(core, l => l.Kind == TwinCatLibraryKind.ResolutionOnly && l.Name == "Tc2_Utilities" && l.Resolution == "Tc2_Utilities, 3.3.47.0 (Beckhoff Automation GmbH)");
    }

    [Fact]
    public void Device_in_own_file_is_kept_as_reference()
    {
        var external = Read(Synthetic).IoDevices.Single(d => d.File is not null);

        Assert.Equal(("Device 2 (EtherCAT).xti", ""), (external.File, external.Name));
        Assert.Empty(external.Boxes);
    }

    [Fact]
    public void Other_xml_is_not_a_tsproj()
    {
        var ex = Assert.Throws<UsageException>(() =>
            TwinCatReader.Read(Fixtures.PathOf("codesys/v35/robin-codesys_project.xml"), Fixtures.Read("codesys/v35/robin-codesys_project.xml"), "robin.xml"));

        Assert.Equal("NOT_TSPROJ", ex.Code);
    }

    [Fact]
    public void Missing_plc_project_ends_with_exit_2()
    {
        var dir = Directory.CreateTempSubdirectory("plc-sbom-");
        try
        {
            var path = Path.Combine(dir.FullName, "x.tsproj");
            File.WriteAllText(path, "<TcSmProject><Project><Plc><Project Name=\"P\" PrjFilePath=\"P\\P.plcproj\"/></Plc></Project></TcSmProject>");

            var ex = Assert.Throws<UsageException>(() => TwinCatReader.Read(path, File.ReadAllBytes(path), "x.tsproj"));

            Assert.Equal("TWINCAT_FILE_NOT_FOUND", ex.Code);
            Assert.Contains("P/P.plcproj", ex.Message);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Missing_independent_plc_file_ends_with_exit_2()
    {
        var dir = Directory.CreateTempSubdirectory("plc-sbom-");
        try
        {
            var path = Path.Combine(dir.FullName, "x.tsproj");
            File.WriteAllText(path, "<TcSmProject><Project><Plc><Project File=\"P.xti\"/></Plc></Project></TcSmProject>");

            var ex = Assert.Throws<UsageException>(() => TwinCatReader.Read(path, File.ReadAllBytes(path), "x.tsproj"));

            Assert.Equal("TWINCAT_FILE_NOT_FOUND", ex.Code);
            Assert.Contains("_Config/PLC/P.xti", ex.Message);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Dtd_is_refused()
    {
        var content = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e \"x\">]><TcSmProject/>"u8.ToArray();

        Assert.Equal("XML_INVALID", Assert.Throws<UsageException>(() => TwinCatReader.Read("x.tsproj", content, "x.tsproj")).Code);
    }
}
