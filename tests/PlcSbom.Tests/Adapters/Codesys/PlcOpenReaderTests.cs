using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.Codesys.V35;

namespace PlcSbom.Tests.Adapters.Codesys;

/// <summary>Every attribute the reader takes is found here in a real fixture (CLAUDE.md section 2).</summary>
public sealed class PlcOpenReaderTests
{
    private const string Robin = "codesys/v35/robin-codesys_project.xml";
    private const string Osc = "codesys/v35/osc-mqtt-OSC_MQTT.xml";
    private const string Ooip = "codesys/v35/ooip-script-engine-example.xml";
    private const string Synthetic = "codesys/v35/zwei-steuerungen-synthetisch.xml";

    private static PlcOpenProject Read(string fixture) => PlcOpenReader.Read(Fixtures.Read(fixture), fixture);

    [Theory]
    [InlineData(Robin, "CODESYS", "CODESYS V3.5 SP15", "2020-03-17T12:31:34.2855802", "Robin example project", "0.0.1")]
    [InlineData(Osc, "CODESYS", "CODESYS V3.5 SP4 Patch 1", "2014-03-07T15:58:01.2038599", "MQTT Library", "1.0.0.0")]
    [InlineData(Ooip, "CODESYS", "CODESYS V3.5 SP20 Patch 1", "2025-05-27T13:20:07.2176472", "Script Engine Example.project", null)]
    public void Reads_file_and_content_header(string fixture, string product, string version, string created, string project, string? projectVersion)
    {
        var p = Read(fixture);

        Assert.Equal(product, p.ProductName);
        Assert.Equal(version, p.ProductVersion);
        Assert.Null(p.CompanyName); // companyName="" in every real export
        Assert.Equal(created, p.CreationDateTime);
        Assert.Equal(project, p.ProjectName);
        Assert.Equal(projectVersion, p.ProjectVersion);
    }

    [Theory]
    [InlineData(Robin, "Device", "4102", "0000 0005", "3.5.15.0")]
    [InlineData(Ooip, "Device", "4097", "0000 000a", "3.5.20.10")]
    public void Reads_the_device_identification(string fixture, string name, string type, string id, string version)
    {
        var device = Assert.Single(Read(fixture).Devices);

        Assert.Equal(name, device.Name);
        Assert.Equal(type, device.Type);
        Assert.Equal(id, device.Id);
        Assert.Equal(version, device.Version);
        Assert.Equal("Application", Assert.Single(device.Applications).Name);
    }

    [Fact]
    public void Configuration_without_device_data_is_no_device()
    {
        // robin: "SoftMotion General Axis Pool" is a nested configuration with only an ExplicitConnector.
        var device = Assert.Single(Read(Robin).Devices);

        Assert.Empty(device.Children);
    }

    [Fact]
    public void Library_project_has_no_device_and_libraries_in_the_pou_pool()
    {
        var p = Read(Osc);

        Assert.Empty(p.Devices);
        Assert.Equal(11, p.ProjectLibraries.Count);
        Assert.All(p.ProjectLibraries, l => Assert.Equal(LibraryResolution.Fixed, l.Resolution));
        Assert.Contains(p.ProjectLibraries, l => l.Reference == "CmpErrors Interfaces, * (System)" && l.Parsed!.IsNewest);
    }

    [Fact]
    public void Placeholder_without_redirection_keeps_the_default_resolution()
    {
        var libraries = Assert.Single(Assert.Single(Read(Robin).Devices).Applications).Libraries;
        var sm3 = libraries.Single(l => l.Reference == "#SM3_Basic");

        Assert.Equal(10, libraries.Count);
        Assert.Equal("SM3_Basic", sm3.Placeholder);
        Assert.Equal("SM3_Basic", sm3.Namespace);
        Assert.True(sm3.SystemLibrary);
        Assert.Equal(LibraryResolution.Default, sm3.Resolution);
        Assert.Equal("SM3_Basic, 4.5.1.0 (3S - Smart Software Solutions GmbH)", sm3.Effective);
        Assert.Null(sm3.Redirection);
    }

    [Fact]
    public void Direct_reference_is_fixed()
    {
        var robin = Assert.Single(Assert.Single(Read(Robin).Devices).Applications).Libraries.Single(l => l.Namespace == "Robin");

        Assert.Equal(LibraryResolution.Fixed, robin.Resolution);
        Assert.False(robin.SystemLibrary);
        Assert.Null(robin.Placeholder);
        Assert.Null(robin.DefaultResolution);
        Assert.Equal(new("Robin", "0.1.2", "INESC TEC"), robin.Parsed);
    }

    [Fact]
    public void Redirection_in_the_same_library_manager_wins_over_the_default()
    {
        var libraries = Assert.Single(Assert.Single(Read(Ooip).Devices).Applications).Libraries;
        var standard = libraries.Single(l => l.Reference == "#Standard");
        var bplog = libraries.Single(l => l.Reference == "#BreakpointLogging");

        Assert.Equal(LibraryResolution.Redirection, standard.Resolution);
        Assert.Equal("Standard, * (System)", standard.DefaultResolution);
        Assert.Equal("Standard, 3.5.18.0 (System)", standard.Redirection);
        Assert.Equal("3.5.18.0", standard.Parsed!.Version);
        Assert.Equal(LibraryResolution.Default, bplog.Resolution);
        Assert.True(bplog.Parsed!.IsNewest);
    }

    [Fact]
    public void Two_controllers_with_nested_device_and_pou_pool()
    {
        var p = Read(Synthetic);

        Assert.Equal(["Verpackung", "Handling"], p.Devices.Select(d => d.Name));
        var verpackung = p.Devices[0];
        Assert.Equal(["Application", "Diagnose"], verpackung.Applications.Select(a => a.Name));
        var child = Assert.Single(verpackung.Children);
        Assert.Equal(("Feldbus_Koppler", "65", "synthetisch_0001", "1.0.0.0"), (child.Name, child.Type, child.Id, child.Version));
        Assert.Equal("CAA Types Extern", Assert.Single(p.ProjectLibraries).Parsed!.Title);
    }

    [Fact]
    public void Not_plcopen_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => PlcOpenReader.Read(Encoding.UTF8.GetBytes("<CAEXFile SchemaVersion=\"2.15\" />"), "x.xml"));

        Assert.Equal("NOT_PLCOPEN", ex.Code);
        Assert.Equal(ExitCodes.Usage, ex.ExitCode);
    }

    [Theory]
    [InlineData("<project xmlns=\"http://www.plcopen.org/xml/tc6_0200\">")]
    [InlineData("kein xml")]
    public void Invalid_xml_ends_with_exit_2(string text)
    {
        var ex = Assert.Throws<UsageException>(() => PlcOpenReader.Read(Encoding.UTF8.GetBytes(text), "x.xml"));

        Assert.Equal("XML_INVALID", ex.Code);
    }

    [Fact]
    public void Dtd_is_refused()
    {
        // External entity: must never be resolved (CLAUDE.md section 2, DtdProcessing.Prohibit).
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE project [ <!ENTITY xxe SYSTEM "file:///etc/passwd"> ]>
            <project xmlns="http://www.plcopen.org/xml/tc6_0200"><fileHeader productName="&xxe;" /></project>
            """;

        var ex = Assert.Throws<UsageException>(() => PlcOpenReader.Read(Encoding.UTF8.GetBytes(xml), "x.xml"));

        Assert.Equal("XML_INVALID", ex.Code);
    }
}
