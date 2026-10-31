using PlcSbom.Core;
using PlcSbom.Core.Adapters.Studio5000;

namespace PlcSbom.Tests.Adapters.Studio5000;

public sealed class L5xReaderTests
{
    private static L5xProject Read(string fixture) =>
        L5xReader.Read(Fixtures.Read("studio5000/" + fixture), fixture);

    [Fact]
    public void Controller_export_header_and_controller()
    {
        var project = Read("ra-ExampleForCICD_L85E.L5X");

        Assert.Equal("36.00", project.SoftwareRevision);
        Assert.Equal("Controller", project.TargetType);
        Assert.Equal("ExampleWithCICD_L85E", project.TargetName);
        Assert.False(project.ContainsContext);
        Assert.Equal("Fri Nov 15 01:11:39 2024", project.ExportDate);
        Assert.Equal(new L5xController("ExampleWithCICD_L85E", "Target", "1756-L85E", "36", "11", RedundancyEnabled: false), project.Controller);
        Assert.Empty(project.AddOnInstructions);
    }

    [Fact]
    public void Controller_module_sits_in_slot_0_of_its_own_backplane()
    {
        var local = Assert.Single(Read("ra-ExampleForCICD_L85E.L5X").Modules);

        Assert.Equal(
            new L5xModule("Local", "1756-L85E", "1", "14", "168", "36", "11", "Local", "1", "Disabled", "ICP", 0),
            local);
    }

    [Fact]
    public void Module_in_the_chassis_has_its_slot_and_keying()
    {
        var en2t = Read("acd-ACDTestsWithAOI.L5X").Modules.Single(m => m.Name == "ETH_MODULE");

        Assert.Equal("1756-EN2T", en2t.CatalogNumber);
        Assert.Equal(("11", "1"), (en2t.Major, en2t.Minor));
        Assert.Equal(("Local", "1"), (en2t.ParentModule, en2t.ParentModPortId));
        Assert.Equal("CompatibleModule", en2t.Ekey);
        Assert.Equal(("ICP", 1), (en2t.UpstreamPortType, en2t.Slot));
    }

    [Fact]
    public void Module_behind_ethernet_has_no_slot()
    {
        var remote = Read("module-baum-synthetisch.L5X").Modules.Single(m => m.Name == "Remote_Rack");

        Assert.Equal("Ethernet", remote.UpstreamPortType);
        Assert.Null(remote.Slot);
    }

    [Fact]
    public void Aoi_with_revision_extension_and_vendor()
    {
        var aoi = Assert.Single(Read("acd-ACDTestsWithAOI.L5X").AddOnInstructions);

        Assert.Equal(new L5xAddOnInstruction("AddOnInstruction", null, "1.7", "ExtendedText", "ACD-TOOLS"), aoi);
    }

    [Theory]
    [InlineData("ra-WetBulbTemperature.L5X", "WetBulbTemperature", "Cardoso&Filho")]
    [InlineData("ra-DelayedSum_AOI.L5X", "DelayedSum", null)]
    public void Aoi_export_has_a_context_controller(string fixture, string name, string? vendor)
    {
        var project = Read(fixture);

        Assert.Equal("AddOnInstructionDefinition", project.TargetType);
        Assert.True(project.ContainsContext);
        Assert.Equal("Context", project.Controller!.Use);
        Assert.Empty(project.Modules);
        var aoi = project.AddOnInstructions.Single(a => a.Use == "Target");
        Assert.Equal((name, "1.0", vendor), (aoi.Name, aoi.Revision, aoi.Vendor));
    }

    [Fact]
    public void Redundancy_flag_is_read()
    {
        Assert.True(Read("acd-ACDTestsEmptyRedundant.L5X").Controller!.RedundancyEnabled);
    }

    [Fact]
    public void Other_xml_is_not_an_l5x()
    {
        var ex = Assert.Throws<UsageException>(() => L5xReader.Read(Fixtures.Read("codesys/v35/robin-codesys_project.xml"), "robin.xml"));

        Assert.Equal("NOT_L5X", ex.Code);
    }

    [Fact]
    public void Dtd_is_refused()
    {
        var content = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e \"x\">]><RSLogix5000Content/>"u8.ToArray();

        Assert.Equal("XML_INVALID", Assert.Throws<UsageException>(() => L5xReader.Read(content, "dtd.L5X")).Code);
    }
}
