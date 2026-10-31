using System.Text.Json.Nodes;
using PlcSbom.Cli;
using PlcSbom.Core;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Cli;

/// <summary>plc-sbom validate end to end: real files in a temporary directory, exit codes, console, result file.</summary>
public sealed class ValidateCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("plc-sbom-test-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static async Task<(int Exit, string Out, string Err)> Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(args, new CliEnvironment(IsWindows: false), stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private string CopyGolden()
    {
        var path = Path.Combine(_dir, "sbom-maschine.cdx.json");
        File.Copy(Golden.PathOf("hand-model.cdx.json"), path);
        return path;
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private JsonObject ResultOf(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(_dir, name)))!.AsObject();

    [Fact]
    public async Task Golden_sbom_passes_the_development_build()
    {
        var (exit, output, error) = await Run("validate", CopyGolden());

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        Assert.Contains("[prüfung]   Schema CycloneDX 1.6 ............ OK", output);
        Assert.Contains("[prüfung]   Pflichtfelder BSI TR-03183-2 .... OK, 9 Warnungen, 32 Hinweise", output);
        Assert.Contains("[ausgabe]   ", output);
    }

    [Fact]
    public async Task Result_file_is_written_next_to_the_sbom()
    {
        await Run("validate", CopyGolden());

        var result = ResultOf("sbom-maschine.result.json");
        Assert.Equal("validate", result["command"]!.GetValue<string>());
        Assert.Equal(0, result["exitCode"]!.GetValue<int>());
        Assert.Equal(9, result["warnings"]!.GetValue<int>());
        Assert.Equal(32, result["notes"]!.GetValue<int>());
        Assert.Equal(2, result["stations"]!.GetValue<int>());
    }

    [Fact]
    public async Task Release_build_turns_warnings_into_a_finding()
    {
        var (exit, output, _) = await Run("validate", CopyGolden(), "--release-build");

        Assert.Equal(ExitCodes.Finding, exit);
        Assert.Contains("NICHT BESTANDEN, 9 Warnungen, 32 Hinweise", output);
        Assert.Equal(1, ResultOf("sbom-maschine.result.json")["exitCode"]!.GetValue<int>());
    }

    [Fact]
    public async Task Notes_are_hidden_without_verbose()
    {
        var (_, output, _) = await Run("validate", CopyGolden());

        Assert.Contains("Warnung: [LICENSE_UNKNOWN]", output);
        Assert.DoesNotContain("Hinweis: [", output);
        Assert.Contains("32 Hinweise ausgeblendet, mit --verbose anzeigen.", output);
    }

    [Theory]
    [InlineData("--verbose")]
    [InlineData("-v")]
    public async Task Verbose_shows_the_notes(string option)
    {
        var (_, output, _) = await Run("validate", CopyGolden(), option);

        Assert.Contains("Hinweis: [HASH_UNAVAILABLE]", output);
        Assert.DoesNotContain("ausgeblendet", output);
    }

    [Fact]
    public async Task Clean_sbom_passes_the_release_build()
    {
        var path = WriteFile("clean.cdx.json", MinimalSbom.Create().ToJsonString());

        var (exit, output, _) = await Run("validate", path, "--release-build");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Contains("OK, 0 Warnungen, 1 Hinweis", output);
    }

    [Fact]
    public async Task Sbom_with_errors_ends_with_exit_1_and_lists_them()
    {
        var bom = MinimalSbom.Create();
        bom["vulnerabilities"] = new JsonArray(new JsonObject { ["id"] = "CVE-2026-0001" });
        var path = WriteFile("vuln.cdx.json", bom.ToJsonString());

        var (exit, output, _) = await Run("validate", path);

        Assert.Equal(ExitCodes.Finding, exit);
        Assert.Contains("NICHT BESTANDEN, 1 Fehler", output);
        Assert.Contains("Fehler: [VULNERABILITIES_PRESENT]", output);
        Assert.Equal(1, ResultOf("vuln.result.json")["errors"]!.GetValue<int>());
    }

    [Fact]
    public async Task Invalid_json_is_a_finding_with_result_file()
    {
        var path = WriteFile("kaputt.json", "{ \"bomFormat\": ");

        var (exit, output, _) = await Run("validate", path);

        Assert.Equal(ExitCodes.Finding, exit);
        Assert.Contains("Fehler: [JSON_INVALID]", output);
        Assert.False(ResultOf("kaputt.result.json")["schemaValid"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Missing_file_is_a_usage_error_without_result_file()
    {
        var path = Path.Combine(_dir, "gibt-es-nicht.cdx.json");

        var (exit, output, error) = await Run("validate", path);

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[SBOM_NOT_FOUND] ", error);
        Assert.Equal("", output);
        Assert.False(File.Exists(Path.Combine(_dir, "gibt-es-nicht.result.json")));
    }

    [Fact]
    public async Task Same_input_gives_the_same_result_file()
    {
        var path = CopyGolden();
        await Run("validate", path);
        var first = File.ReadAllBytes(Path.Combine(_dir, "sbom-maschine.result.json"));

        await Run("validate", path);

        Assert.Equal(first, File.ReadAllBytes(Path.Combine(_dir, "sbom-maschine.result.json")));
    }
}
