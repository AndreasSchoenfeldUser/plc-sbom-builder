using PlcSbom.Cli;
using PlcSbom.Core;

namespace PlcSbom.Tests.Cli;

public sealed class CliAppTests
{
    private static readonly CliEnvironment Linux = new(IsWindows: false);
    private static readonly CliEnvironment Windows = new(IsWindows: true);

    private static async Task<(int Exit, string Out, string Err)> Run(CliEnvironment environment, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(args, environment, stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Exit_codes_match_the_documented_interface()
    {
        // CLAUDE.md section 7.2; changing a value needs an entry in docs/entscheidungen.md.
        Assert.Equal(0, ExitCodes.Ok);
        Assert.Equal(1, ExitCodes.Finding);
        Assert.Equal(2, ExitCodes.Usage);
        Assert.Equal(3, ExitCodes.Environment);
        Assert.Equal(4, ExitCodes.Aborted);
    }

    [Fact]
    public async Task Help_lists_all_commands()
    {
        var (exit, output, _) = await Run(Linux, "--help");

        Assert.Equal(ExitCodes.Ok, exit);
        foreach (var command in new[] { "build", "validate", "report", "compare", "ids", "inspect", "export-aml", "doctor" })
        {
            Assert.Contains(command, output);
        }
        Assert.Contains("--verbose", output);
        Assert.Contains("--timeout", output);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("validate")]
    [InlineData("report")]
    [InlineData("compare")]
    [InlineData("ids")]
    [InlineData("inspect")]
    [InlineData("export-aml")]
    [InlineData("doctor")]
    public async Task Help_of_each_command_succeeds(string command)
    {
        var (exit, output, _) = await Run(Linux, command, "--help");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Contains(command, output);
    }

    [Fact]
    public async Task Build_help_shows_the_options_of_section_7_1()
    {
        var (_, output, _) = await Run(Linux, "build", "--help");

        foreach (var option in new[] { "--config", "--release", "--commit", "--out", "--report", "--release-build" })
        {
            Assert.Contains(option, output);
        }
    }

    [Fact]
    public async Task Version_prints_the_tool_version()
    {
        var (exit, output, _) = await Run(Linux, "--version");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.StartsWith(ToolVersion.Short, output.Trim());
        Assert.Matches(@"^\d+\.\d+\.\d+$", ToolVersion.Short);
    }

    [Fact]
    public async Task No_command_is_a_usage_error()
    {
        var (exit, _, error) = await Run(Linux);

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("plc-sbom --help", error);
    }

    [Theory]
    [InlineData("unbekannt")]
    [InlineData("build", "--release", "R1")]                                   // --config missing
    [InlineData("build", "--config", "machine.yml")]                            // --release missing
    [InlineData("build", "--config", "m.yml", "--release", "R1", "--report", "pdf")]
    [InlineData("build", "--config", "m.yml", "--release", "R1", "--unbekannt")]
    [InlineData("report", "sbom.json", "--format", "pdf", "--out", "x")]
    [InlineData("inspect", "a.aml", "--adapter", "s7-classic")]                 // unknown adapter id
    [InlineData("inspect", "a.aml")]                                            // --adapter missing
    [InlineData("doctor", "--adapter", "twincat")]
    [InlineData("validate")]                                                    // sbom argument missing
    [InlineData("build", "--config", "m.yml", "--release", "R1", "--timeout", "0")]
    [InlineData("build", "--config", "m.yml", "--release", "R1", "--timeout", "zehn")]
    public async Task Invalid_calls_end_with_exit_2(params string[] args)
    {
        var (exit, _, error) = await Run(Linux, args);

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[USAGE] ", error);
    }

    [Theory]
    [InlineData("M9", "build", "--config", "machine.yml", "--release", "R2026.10")]
    [InlineData("M1", "validate", "build/sbom-maschine.cdx.json", "--release-build")]
    [InlineData("M10", "report", "sbom.json", "--format", "md", "--out", "bericht.md")]
    [InlineData("M11", "compare", "sbom.json", "--field", "feld/1042.json", "--csv", "x.csv")]
    [InlineData("M12", "ids", "sbom.json", "--csaf", "csaf/")]
    [InlineData("M2", "inspect", "projekt.xml", "--adapter", "codesys")]
    [InlineData("M14", "doctor")]
    public async Task Commands_not_yet_built_name_their_milestone(string milestone, params string[] args)
    {
        var (exit, _, error) = await Run(Linux, args);

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[NOT_IMPLEMENTED] ", error);
        Assert.Contains($"Meilenstein {milestone}.", error);
    }

    [Theory]
    [InlineData("export-aml", "--project", "tia/Linie1.zap21", "--out", "export/Linie1.aml")]
    [InlineData("doctor", "--adapter", "tia-portal")]
    [InlineData("doctor", "--adapter", "tia-portal", "--deep")]
    public async Task Openness_commands_off_windows_are_an_environment_problem(params string[] args)
    {
        var (exit, _, error) = await Run(Linux, args);

        Assert.Equal(ExitCodes.Environment, exit);
        Assert.StartsWith("[OPENNESS_WINDOWS_ONLY] ", error);
        Assert.Contains("TIA Portal V21", error);
    }

    [Fact]
    public async Task Openness_commands_on_windows_pass_the_platform_check()
    {
        var (exit, _, error) = await Run(Windows, "export-aml", "--project", "a.zap21", "--out", "a.aml");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("Meilenstein M14.", error);
    }

    [Fact]
    public async Task Common_options_are_accepted_after_the_command()
    {
        var (exit, _, error) = await Run(Linux, "build", "--config", "m.yml", "--release", "R1", "--verbose", "--timeout", "30");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[NOT_IMPLEMENTED] ", error);
    }

    [Fact]
    public async Task Known_errors_map_to_their_exit_code()
    {
        var stderr = new StringWriter();

        var exit = await CliApp.ExecuteAsync(
            () => throw new EnvironmentException("TIA_NOT_INSTALLED", "TIA Portal V21 ist nicht installiert."),
            stderr,
            verbose: false);

        Assert.Equal(ExitCodes.Environment, exit);
        Assert.Equal("[TIA_NOT_INSTALLED] TIA Portal V21 ist nicht installiert.", stderr.ToString().Trim());
    }

    [Fact]
    public async Task Unexpected_errors_abort_with_exit_4()
    {
        var stderr = new StringWriter();

        var exit = await CliApp.ExecuteAsync(() => throw new InvalidOperationException("kaputt"), stderr, verbose: false);

        Assert.Equal(ExitCodes.Aborted, exit);
        Assert.StartsWith("[UNEXPECTED] ", stderr.ToString());
        Assert.DoesNotContain("   at ", stderr.ToString());
    }

    [Fact]
    public async Task Verbose_shows_the_stack_trace_of_unexpected_errors()
    {
        var stderr = new StringWriter();

        await CliApp.ExecuteAsync(() => throw new InvalidOperationException("kaputt"), stderr, verbose: true);

        Assert.Contains("InvalidOperationException", stderr.ToString());
    }
}
