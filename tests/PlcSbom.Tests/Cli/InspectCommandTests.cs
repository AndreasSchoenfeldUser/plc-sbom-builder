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
