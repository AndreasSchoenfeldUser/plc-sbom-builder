using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.Codesys;

namespace PlcSbom.Tests.Adapters.Codesys;

public sealed class DeviceDescriptionIndexTests
{
    private static DeviceDescriptionIndex Load() =>
        DeviceDescriptionIndex.Load([(Fixtures.PathOf("codesys/devdesc"), "codesys/devdesc")]);

    [Theory]
    // Values as written in fixtures/codesys/devdesc/ (stefandreyer/I2C-Multiplex).
    [InlineData("TCA9548.devdesc.xml", "500", "FFFF 0001", "1.0.1.1", "TCA9548")]
    [InlineData("PCF8575IN.devdesc.xml", "500", "FFFF 0007", "1.0.1.1", "PCF8575IN")]
    [InlineData("SSD1306.devdesc.xml", "500", "FFFF 0007", "1.0.1.1", "SSD1306")]
    [InlineData("TSL2561.devdesc.xml", "500", "FFFF 0006", "1.0.1.1", "TSL2561")]
    public void Reads_identification_name_and_vendor(string file, string type, string id, string version, string name)
    {
        var d = Assert.Single(DeviceDescriptionIndex.Read(Fixtures.Read($"codesys/devdesc/{file}"), file));

        Assert.Equal((type, id, version, name), (d.Type, d.Id, d.Version, d.Name));
        Assert.Equal("Stefan Dreyer", d.Vendor);
        Assert.Null(d.OrderNumber); // every fixture writes "-"
        Assert.Equal(64, d.Sha256.Length);
    }

    [Fact]
    public void Load_reads_all_files_sorted_with_repository_paths()
    {
        var index = Load();

        Assert.Equal(
            ["codesys/devdesc/PCF8575IN.devdesc.xml", "codesys/devdesc/SSD1306.devdesc.xml", "codesys/devdesc/TCA9548.devdesc.xml", "codesys/devdesc/TSL2561.devdesc.xml"],
            index.All.Select(d => d.File));
    }

    [Fact]
    public void Unique_identification_is_found_case_insensitive_in_the_id()
    {
        var match = Load().Find("500", "ffff 0001", "1.0.1.1");

        Assert.Equal("TCA9548", match.Found!.Name);
        Assert.False(match.IsAmbiguous);
    }

    [Fact]
    public void Shared_identification_is_ambiguous()
    {
        var match = Load().Find("500", "FFFF 0007", "1.0.1.1");

        Assert.Null(match.Found);
        Assert.True(match.IsAmbiguous);
        Assert.Equal(["PCF8575IN", "SSD1306"], match.Candidates.Select(c => c.Name));
    }

    [Theory]
    [InlineData("500", "FFFF 0006", "1.0.0.0")] // TSL2561 exists only as 1.0.1.1
    [InlineData("4096", "0000 0001", "3.5.21.0")]
    [InlineData(null, "FFFF 0001", "1.0.1.1")]
    public void Other_identification_is_not_found(string? type, string id, string version)
    {
        var match = Load().Find(type, id, version);

        Assert.Null(match.Found);
        Assert.False(match.IsAmbiguous);
    }

    [Fact]
    public void Missing_directory_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => DeviceDescriptionIndex.Load([(Fixtures.PathOf("codesys/gibt-es-nicht"), "codesys/gibt-es-nicht")]));

        Assert.Equal("DEVDESC_DIR_NOT_FOUND", ex.Code);
    }

    [Fact]
    public void Other_xml_is_no_device_description()
    {
        var ex = Assert.Throws<UsageException>(() => DeviceDescriptionIndex.Read(Encoding.UTF8.GetBytes("<project />"), "x.devdesc.xml"));

        Assert.Equal("NOT_DEVDESC", ex.Code);
    }
}
