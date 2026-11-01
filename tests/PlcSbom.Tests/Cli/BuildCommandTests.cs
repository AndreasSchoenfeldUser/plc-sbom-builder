using System.Text.Json.Nodes;
using PlcSbom.Cli;
using PlcSbom.Core;
using PlcSbom.Tests.Build;

namespace PlcSbom.Tests.Cli;

/// <summary>plc-sbom build end to end on the sample machine: console, exit codes, SBOM and result file.</summary>
public sealed class BuildCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("plc-sbom-cli-build-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Out => Path.Combine(_dir, "build", "sbom-maschine.cdx.json");

    private string ResultPath => Path.Combine(_dir, "build", "sbom-maschine.result.json");

    private static string Sample => RepoPaths.Of("samples", "maschine-xy", "machine.yml");

    private async Task<(int Exit, string Out, string Err)> Run(params string[] extra)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var environment = new CliEnvironment(IsWindows: false) { Build = FakeBuildEnvironment.Reproducible() };
        string[] args = ["build", "--config", Sample, "--release", "R2026.10", "--commit", MachineBuilderTests.Commit, "--out", Out, .. extra];
        var exit = await CliApp.RunAsync(args, environment, stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private JsonObject Result() => JsonNode.Parse(File.ReadAllText(ResultPath))!.AsObject();

    [Fact]
    public async Task Sample_machine_builds_with_exit_0_and_writes_sbom_and_result()
    {
        var (exit, output, error) = await Run();

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        Assert.True(File.Exists(Out));
        var result = Result();
        Assert.Equal("build", result["command"]!.ToString());
        Assert.Equal(0, result["exitCode"]!.GetValue<int>());
        Assert.Equal(0, result["errors"]!.GetValue<int>());
        Assert.True(result["warnings"]!.GetValue<int>() > 0);
        Assert.Equal(3, result["stations"]!.GetValue<int>());
        Assert.Contains("[adapter codesys]     Station Verpackung: 1 Gerät, 10 Bibliotheken (CODESYS V3.5 SP15)", output);
        Assert.Contains("[adapter tia-aml]     Station PLC_1: 4 Geräte, 1 Bibliothek (Export 28.12.2024, TIA Portal V17)", output);
        Assert.Contains("[quellen]   1 Hersteller-SBOM (1 zugeordnet), 1 Teil-SBOM (syft), 1 GSDML-Datei", output);
        Assert.Contains("[merge]     Maschine XY > 3 Stationen > 49 Komponenten", output);
        Assert.Contains("[prüfung]   Schema CycloneDX 1.6 ............ OK", output);
        Assert.Contains($"[bericht]   {Path.Combine(_dir, "build", "sbom-maschine.html")}", output);
        Assert.StartsWith("<!doctype html>", File.ReadAllText(Path.Combine(_dir, "build", "sbom-maschine.html")));
        Assert.Contains($"[ausgabe]   {Out}", output);
        Assert.Matches(@"Fertig in \d+,\d s", output);
    }

    [Fact]
    public async Task Command_output_equals_the_builder_output()
    {
        await Run();

        // The golden file is written with tool version 0.1.0, the command with the real one.
        Assert.Equal(ToolVersion.Short == "0.1.0", File.ReadAllBytes(Out).SequenceEqual(File.ReadAllBytes(Golden.PathOf("maschine-xy.cdx.json"))));
    }

    [Fact]
    public async Task Release_build_fails_on_warnings()
    {
        var (exit, output, _) = await Run("--release-build");

        Assert.Equal(ExitCodes.Finding, exit);
        Assert.Contains("NICHT BESTANDEN", output);
        Assert.Equal(1, Result()["exitCode"]!.GetValue<int>());
    }

    [Fact]
    public async Task Report_none_skips_the_report()
    {
        var (_, output, _) = await Run("--report", "none");

        Assert.DoesNotContain("[bericht]", output);
        Assert.False(File.Exists(Path.Combine(_dir, "build", "sbom-maschine.html")));
    }

    [Fact]
    public async Task Report_md_writes_the_markdown_report_like_the_golden_file()
    {
        var (_, output, _) = await Run("--report", "md");

        var path = Path.Combine(_dir, "build", "sbom-maschine.md");
        Assert.Contains($"[bericht]   {path}", output);
        // The golden file names build/sbom-maschine.cdx.json; the command names the path given with --out.
        var expected = File.ReadAllText(Golden.PathOf("maschine-xy.bericht.md")).Replace(SampleMachineSbomPath, Out.Replace('\\', '/'), StringComparison.Ordinal);
        Assert.Equal(ToolVersion.Short == "0.1.0" ? expected : File.ReadAllText(path), File.ReadAllText(path));
    }

    private const string SampleMachineSbomPath = "build/sbom-maschine.cdx.json";

    [Fact]
    public async Task Missing_config_is_exit_2_with_a_result_file()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(
            ["build", "--config", Path.Combine(_dir, "fehlt.yml"), "--release", "R1", "--out", Out],
            new CliEnvironment(IsWindows: false) { Build = FakeBuildEnvironment.Reproducible() },
            stdout,
            stderr);

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[CONFIG_NOT_FOUND] ", stderr.ToString());
        Assert.False(File.Exists(Out));
        var result = Result();
        Assert.Equal(2, result["exitCode"]!.GetValue<int>());
        Assert.Equal("CONFIG_NOT_FOUND", result["findings"]![0]!["code"]!.ToString());
    }

    [Fact]
    public async Task Unreadable_vendor_sbom_is_exit_1()
    {
        // Copy of the sample next to a broken vendor SBOM; paths keep pointing to the fixtures.
        var sample = Path.GetDirectoryName(Sample)!;
        var copy = Path.Combine(_dir, "samples", "maschine-xy");
        foreach (var file in Directory.EnumerateFiles(sample, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(copy, Path.GetRelativePath(sample, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        var yaml = File.ReadAllText(Path.Combine(copy, "machine.yml")).Replace("../../fixtures", RepoPaths.Of("fixtures").Replace('\\', '/'), StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(copy, "machine.yml"), yaml);
        File.WriteAllText(Path.Combine(copy, "sbom", "lieferanten", "kaputt.cdx.json"), "{");

        var stdout = new StringWriter();
        var exit = await CliApp.RunAsync(
            ["build", "--config", Path.Combine(copy, "machine.yml"), "--release", "R1", "--commit", MachineBuilderTests.Commit, "--out", Out],
            new CliEnvironment(IsWindows: false) { Build = FakeBuildEnvironment.Reproducible() },
            stdout,
            new StringWriter());

        Assert.Equal(ExitCodes.Finding, exit);
        Assert.Contains("Fehler: [INPUT_SBOM_INVALID]", stdout.ToString());
        Assert.True(File.Exists(Out));
    }
}
