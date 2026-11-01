using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PlcSbom.Cli.Commands;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;

namespace PlcSbom.Tests.Docs;

/// <summary>
/// examples/Jenkinsfile and the CI workflow against the interfaces they rely on (CLAUDE.md 7.2, 7.3, 10, 11):
/// result file fields, output paths, release gate, exit codes, the SBOM of the package.
/// </summary>
public sealed partial class PipelineExampleTests
{
    private static string Jenkinsfile => File.ReadAllText(RepoPaths.Of("examples/Jenkinsfile"));

    private static string Workflow => File.ReadAllText(RepoPaths.Of(".github/workflows/ci.yml"));

    private static string Env(string name) =>
        Assert.Single(EnvLine().Matches(Jenkinsfile), m => m.Groups["name"].Value == name).Groups["value"].Value.Trim('\'');

    [Fact]
    public void Jenkinsfile_reads_only_fields_the_result_file_has()
    {
        var report = new ValidationReport
        {
            SpecVersion = "1.6",
            SchemaValid = true,
            Findings = [new(Severity.Error, "CONFIG_NOT_FOUND", "fehlt")],
        };
        var result = JsonNode.Parse(ResultFile.Write("build", "build/sbom-maschine.cdx.json", report, 2))!.AsObject();
        var read = ResultField().Matches(Jenkinsfile).Select(m => m.Groups["field"].Value).Distinct().ToList();

        Assert.Contains("errors", read);
        Assert.Contains("warnings", read);
        Assert.All(read, field => Assert.True(result.ContainsKey(field), $"Die Ergebnisdatei hat kein Feld „{field}“."));
        Assert.Contains("result.findings[0].code", Jenkinsfile);
        Assert.NotNull(result["findings"]![0]!["code"]);
    }

    [Fact]
    public void Jenkinsfile_paths_match_the_outputs_of_build()
    {
        var sbom = Env("SBOM");

        Assert.Equal("build/sbom-maschine.cdx.json", sbom);
        Assert.Equal(ResultFile.PathFor(sbom).Replace('\\', '/'), Env("SBOM_RESULT"));
        var report = CommandFiles.ReportPathFor(sbom, "html").Replace('\\', '/');
        Assert.Matches(@"^build/sbom-[^/]*\.html$", report);
        Assert.Contains("archiveArtifacts artifacts: 'build/sbom-*.cdx.json, build/sbom-*.html, build/*.result.json'", Jenkinsfile);
    }

    [Fact]
    public void Jenkinsfile_gates_release_builds_on_the_tag()
    {
        Assert.Contains("def gate = env.TAG_NAME ? '--release-build' : ''", Jenkinsfile);
        Assert.Contains("TAG_NAME ?: 'dev-' + env.BUILD_NUMBER", Env("RELEASE"));
        Assert.Contains("unstable \"SBOM mit ${result.warnings} Warnungen", Jenkinsfile);
    }

    [Fact]
    public void Jenkinsfile_removes_outputs_of_an_earlier_run_but_not_all_of_build()
    {
        var script = Jenkinsfile;
        var cleanup = script.IndexOf("sh \"rm -f ${stem}.*\"", StringComparison.Ordinal);

        Assert.True(cleanup > 0, "Das Jenkinsfile entfernt alte Ausgaben nicht.");
        Assert.True(cleanup < script.IndexOf("returnStatus: true", StringComparison.Ordinal), "Alte Ausgaben müssen vor dem Aufruf entfernt werden.");
        Assert.Contains("def stem = env.SBOM - '.cdx.json'", script);
        Assert.DoesNotContain("deleteDir", script);
    }

    [Fact]
    public void Jenkinsfile_treats_an_empty_tag_as_development_build()
    {
        Assert.Contains("expression { env.TAG_NAME ? true : false }", Jenkinsfile);
        Assert.DoesNotContain("TAG_NAME != null", Jenkinsfile);
    }

    [Fact]
    public void Jenkinsfile_handles_every_exit_code()
    {
        foreach (var exit in new[] { 0, 1, 2, 3 })
        {
            Assert.Contains($"case {exit}:", Jenkinsfile);
        }
        Assert.Contains("default:", Jenkinsfile);
        Assert.Contains("returnStatus: true", Jenkinsfile);
    }

    [Fact]
    public void Jenkinsfile_keeps_the_dependency_track_key_in_a_credential()
    {
        Assert.Contains("dependencyTrackApiKey: env.DTRACK_CREDENTIALS", Jenkinsfile);
        Assert.Matches("^[a-z0-9-]+$", Env("DTRACK_CREDENTIALS"));
        Assert.Contains("projectVersion: env.RELEASE", Jenkinsfile);
    }

    [Fact]
    public void Ci_puts_the_tool_sbom_into_the_package_and_checks_it_with_the_release_gate()
    {
        Assert.Contains("dotnet run tools/tool-sbom.cs -- --exe \"$exe\" --out out/pkg/plc-sbom.cdx.json", Workflow);
        Assert.Contains("expect 0 validate out/tool-sbom.cdx.json --release-build", Workflow);
        Assert.Contains("out/pkg/plc-sbom.cdx.json", Workflow[Workflow.IndexOf("crosscheck:", StringComparison.Ordinal)..]);
        Assert.Contains("sha256sum plc-sbom-* | sort -k2 > SHA256SUMS", Workflow);
    }

    [GeneratedRegex(@"(?<![.\w-])result\.(?<field>[a-zA-Z]+)\b")]
    private static partial Regex ResultField();

    [GeneratedRegex(@"^\s+(?<name>[A-Z_]+)\s+=\s+(?<value>.+?)\s*(?://.*)?$", RegexOptions.Multiline)]
    private static partial Regex EnvLine();
}
