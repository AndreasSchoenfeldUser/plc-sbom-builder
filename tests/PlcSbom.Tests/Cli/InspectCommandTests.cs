using System.Text;
using System.Text.Json.Nodes;
using PlcSbom.Cli;
using PlcSbom.Core;

namespace PlcSbom.Tests.Cli;

public sealed class InspectCommandTests
{
    private static async Task<(int Exit, string Out, string Err)> Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(args, new CliEnvironment(IsWindows: false), stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public async Task Codesys_export_gives_the_raw_inventory_as_json()
    {
        var path = Fixtures.PathOf("codesys/v35/robin-codesys_project.xml");

        var (exit, output, error) = await Run("inspect", path, "--adapter", "codesys");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        var json = JsonNode.Parse(output)!;
        Assert.Equal("codesys", json["adapter"]!.GetValue<string>());
        Assert.Equal("v35", json["source"]!.GetValue<string>());
        Assert.Equal("acf12d77f7ad9ba162485e9c8d64545d97ab37e81f396507112917330af17f2d", json["sha256"]!.GetValue<string>());
        var device = json["project"]!["devices"]![0]!;
        Assert.Equal("3.5.15.0", device["version"]!.GetValue<string>());
        Assert.Equal("default", device["applications"]![0]!["libraries"]![0]!["resolution"]!.GetValue<string>());
    }

    [Fact]
    public async Task Inventory_matches_the_golden_file()
    {
        var fixture = Fixtures.PathOf("codesys/v35/zwei-steuerungen-synthetisch.xml");

        var (exit, output, _) = await Run("inspect", fixture, "--adapter", "codesys");

        Assert.Equal(ExitCodes.Ok, exit);
        // The file field echoes the path as given; replaced so the golden file does not depend on the checkout.
        var normalized = output.Replace(fixture.Replace("\\", "\\\\"), "fixtures/codesys/v35/zwei-steuerungen-synthetisch.xml");
        Golden.AssertMatches("codesys-v35-zwei-steuerungen.inspect.json", Encoding.UTF8.GetBytes(normalized));
    }

    [Fact]
    public async Task Studio5000_inventory_matches_the_golden_file()
    {
        var fixture = Fixtures.PathOf("studio5000/module-baum-synthetisch.L5X");

        var (exit, output, error) = await Run("inspect", fixture, "--adapter", "studio5000");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        Assert.DoesNotContain("192.0.2.", output); // port addresses never leave the reader
        var normalized = output.Replace(fixture.Replace("\\", "\\\\"), "fixtures/studio5000/module-baum-synthetisch.L5X");
        Golden.AssertMatches("studio5000-module-baum.inspect.json", Encoding.UTF8.GetBytes(normalized));
    }

    [Fact]
    public async Task Studio5000_component_export_can_be_inspected()
    {
        var (exit, output, _) = await Run("inspect", Fixtures.PathOf("studio5000/ra-WetBulbTemperature.L5X"), "--adapter", "studio5000");

        Assert.Equal(ExitCodes.Ok, exit);
        var json = JsonNode.Parse(output)!;
        Assert.Equal("studio5000", json["adapter"]!.GetValue<string>());
        Assert.Equal("AddOnInstructionDefinition", json["project"]!["targetType"]!.GetValue<string>());
    }

    [Fact]
    public async Task Twincat_inventory_matches_the_golden_file()
    {
        var fixture = Fixtures.PathOf("twincat/spt-ethercat-diagnostics/SPT EtherCAT Diagnostics.tsproj");

        var (exit, output, error) = await Run("inspect", fixture, "--adapter", "twincat");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        Assert.DoesNotContain("192.168.8.100", output); // AmsNetId, TargetNetId never leave the reader
        var normalized = output.Replace(fixture.Replace("\\", "\\\\"), "fixtures/twincat/spt-ethercat-diagnostics/SPT EtherCAT Diagnostics.tsproj");
        Golden.AssertMatches("twincat-spt-ethercat-diagnostics.inspect.json", Encoding.UTF8.GetBytes(normalized));
    }

    [Fact]
    public async Task Tia_aml_inventory_matches_the_golden_file()
    {
        var fixture = Fixtures.PathOf("tia-aml/zwei-cpus-synthetisch.aml");

        var (exit, output, error) = await Run("inspect", fixture, "--adapter", "tia-aml");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.DoesNotContain("192.0.2.", output); // network addresses never leave the reader
        // Warnings go to stderr as well as into the JSON.
        Assert.StartsWith("[TIA_AML_CATALOG_VERSION_DIFFERS] ", error);
        var normalized = output.Replace(fixture.Replace("\\", "\\\\"), "fixtures/tia-aml/zwei-cpus-synthetisch.aml");
        Golden.AssertMatches("tia-aml-zwei-cpus.inspect.json", Encoding.UTF8.GetBytes(normalized));
    }

    [Fact]
    public async Task Tia_aml_inventory_shows_cpus_and_assignment()
    {
        var (exit, output, error) = await Run("inspect", Fixtures.PathOf("tia-aml/plc-commissioning-sample_device.aml"), "--adapter", "tia-aml");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        var inspection = JsonNode.Parse(output)!["inspection"]!;
        Assert.Equal("V17", inspection["tool"]!["version"]!.GetValue<string>());
        Assert.Equal("PLC_1", inspection["cpus"]![0]!["name"]!.GetValue<string>());
        var gsd = inspection["devices"]![0]!;
        Assert.Equal("GSD device_1", gsd["name"]!.GetValue<string>());
        Assert.Empty(gsd["assignedTo"]!.AsArray());
        Assert.Equal("gsdDevice", gsd["items"]![1]!["treatment"]!.GetValue<string>());
    }

    [Fact]
    public async Task Non_aml_file_ends_with_exit_2()
    {
        var (exit, _, error) = await Run("inspect", Fixtures.PathOf("codesys/v35/robin-codesys_project.xml"), "--adapter", "tia-aml");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[NOT_AML] ", error);
    }

    [Fact]
    public async Task Gsdml_adds_vendor_and_order_number_to_gsd_items()
    {
        var gsdml = Fixtures.PathOf("gsdml");

        var (exit, output, error) = await Run("inspect", Fixtures.PathOf("tia-aml/plc-commissioning-sample_device.aml"), "--adapter", "tia-aml", "--gsdml", gsdml);

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        var items = JsonNode.Parse(output)!["inspection"]!["devices"]![0]!["items"]!.AsArray();
        var dap = items.Single(i => i!["treatment"]!.GetValue<string>() == "gsdDevice")!["gsdml"]!;
        Assert.Equal("Leuze electronic GmbH + Co. KG", dap["vendor"]!.GetValue<string>());
        Assert.Equal("501xxxxx", dap["orderNumber"]!.GetValue<string>());
        Assert.Equal("V1.*.*", dap["softwareRelease"]!.GetValue<string>());
        Assert.EndsWith("GSDML-V2.41-LEUZE-BCL248i-20211213.xml", dap["file"]!.GetValue<string>());
        Assert.All(items.Where(i => i!["treatment"]!.GetValue<string>() == "gsdModule"), m => Assert.NotNull(m!["gsdml"]));
        Assert.All(items.Where(i => i!["treatment"]!.GetValue<string>() == "structure"), m => Assert.Null(m!["gsdml"]));
    }

    [Fact]
    public async Task Gsdml_not_found_is_a_warning_on_stderr()
    {
        var (exit, output, error) = await Run("inspect", Fixtures.PathOf("tia-aml/plc-commissioning-sample_device.aml"), "--adapter", "tia-aml", "--gsdml", Fixtures.PathOf("codesys/devdesc"));

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("[GSDML_NOT_FOUND] Die GSDML-Datei GSDML-V2.41-LEUZE-BCL248I-20211213.XML liegt in keinem der Verzeichnisse aus „gsdml“.\n", error.ReplaceLineEndings("\n"));
        Assert.Equal("GSDML_NOT_FOUND", JsonNode.Parse(output)!["inspection"]!["warnings"]![0]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Missing_gsdml_directory_ends_with_exit_2()
    {
        var (exit, _, error) = await Run("inspect", Fixtures.PathOf("tia-aml/plc-commissioning-sample_device.aml"), "--adapter", "tia-aml", "--gsdml", Fixtures.PathOf("gibt-es-nicht"));

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[GSDML_DIR_NOT_FOUND] ", error);
    }

    [Fact]
    public async Task Non_tsproj_file_ends_with_exit_2()
    {
        var (exit, _, error) = await Run("inspect", Fixtures.PathOf("codesys/v35/robin-codesys_project.xml"), "--adapter", "twincat");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[NOT_TSPROJ] ", error);
    }

    [Fact]
    public async Task Non_l5x_file_ends_with_exit_2()
    {
        var (exit, _, error) = await Run("inspect", Fixtures.PathOf("codesys/v35/robin-codesys_project.xml"), "--adapter", "studio5000");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[NOT_L5X] ", error);
    }

    [Fact]
    public async Task Missing_file_ends_with_exit_2()
    {
        var (exit, _, error) = await Run("inspect", "gibt-es-nicht.xml", "--adapter", "codesys");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[SOURCE_NOT_FOUND] ", error);
    }

    [Fact]
    public async Task Non_plcopen_file_ends_with_exit_2()
    {
        var path = Path.Combine(Path.GetTempPath(), $"plc-sbom-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(path, "<CAEXFile SchemaVersion=\"2.15\" />", TestContext.Current.CancellationToken);
        try
        {
            var (exit, _, error) = await Run("inspect", path, "--adapter", "codesys");

            Assert.Equal(ExitCodes.Usage, exit);
            Assert.StartsWith("[NOT_PLCOPEN] ", error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Gsdml_is_refused_for_codesys()
    {
        var (exit, _, error) = await Run("inspect", Fixtures.PathOf("codesys/v35/robin-codesys_project.xml"), "--adapter", "codesys", "--gsdml", "gsdml/");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[USAGE] ", error);
    }
}
