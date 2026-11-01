using System.CommandLine;
using System.Globalization;
using PlcSbom.Cli.Commands;
using PlcSbom.Core;

namespace PlcSbom.Cli;

/// <summary>
/// Command line of plc-sbom (CLAUDE.md section 7.1). Testable: process state comes in through
/// <see cref="CliEnvironment"/>, output through the writers.
/// </summary>
public static class CliApp
{
    public static async Task<int> RunAsync(string[] args, CliEnvironment environment, TextWriter stdout, TextWriter stderr)
    {
        var verbose = new Option<bool>("--verbose", "-v") { Description = "Diagnose auf stderr ausgeben", Recursive = true };
        var timeout = new Option<int?>("--timeout")
        {
            Description = "Zeitlimit für den Openness-Worker in Minuten",
            Recursive = true,
            HelpName = "min",
            CustomParser = result =>
            {
                var text = result.Tokens.Count == 1 ? result.Tokens[0].Value : "";
                if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes > 0)
                {
                    return minutes;
                }
                result.AddError($"--timeout erwartet eine positive ganze Zahl von Minuten, nicht „{text}“.");
                return null;
            },
        };

        var root = new RootCommand("PLC SBOM Builder: erzeugt aus den SPS-Projekten einer Maschine eine SBOM im Format CycloneDX 1.6 und prüft sie gegen BSI TR-03183-2.");
        root.Options.Add(verbose);
        root.Options.Add(timeout);

        Func<ParseResult, CancellationToken, Task<int>> Guard(Func<ParseResult, Task<int>> action) =>
            (parse, _) => ExecuteAsync(() => action(parse), stderr, parse.GetValue(verbose));

        root.Subcommands.Add(Build(Guard, environment, verbose, stdout));
        root.Subcommands.Add(Validate(Guard, verbose, stdout));
        root.Subcommands.Add(Report(Guard, stdout));
        root.Subcommands.Add(Compare(Guard, environment, verbose, stdout));
        root.Subcommands.Add(Ids(Guard, verbose, stdout, stderr));
        root.Subcommands.Add(Inspect(Guard, stdout, stderr));
        root.Subcommands.Add(ExportAml(Guard, environment));
        root.Subcommands.Add(Doctor(Guard, environment));

        var parse = root.Parse(args);
        if (parse.Errors.Count > 0)
        {
            foreach (var error in parse.Errors)
            {
                await stderr.WriteLineAsync(Messages.Format("USAGE", error.Message));
            }
            await stderr.WriteLineAsync("Hilfe: plc-sbom --help");
            return ExitCodes.Usage;
        }
        return await parse.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr });
    }

    /// <summary>Runs a command and maps exceptions to exit codes (section 7.2).</summary>
    internal static async Task<int> ExecuteAsync(Func<Task<int>> action, TextWriter stderr, bool verbose)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (PlcSbomException ex)
        {
            await stderr.WriteLineAsync(ex.UserMessage);
            if (verbose && ex.InnerException is not null)
            {
                await stderr.WriteLineAsync(ex.InnerException.ToString());
            }
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync(Messages.Format("UNEXPECTED", $"Unerwarteter Fehler: {ex.Message}"));
            if (verbose)
            {
                await stderr.WriteLineAsync(ex.ToString());
            }
            return ExitCodes.Aborted;
        }
    }

    private static Command Build(
        Func<Func<ParseResult, Task<int>>, Func<ParseResult, CancellationToken, Task<int>>> guard,
        CliEnvironment environment,
        Option<bool> verbose,
        TextWriter stdout)
    {
        var config = new Option<string>("--config") { Description = "Maschinenbeschreibung machine.yml", Required = true };
        var release = new Option<string>("--release") { Description = "Release-Kennung, z. B. R2026.10", Required = true };
        var commit = new Option<string?>("--commit") { Description = "Git-Commit (Vorgabe: GIT_COMMIT, sonst git rev-parse HEAD)", HelpName = "sha" };
        var output = new Option<string>("--out") { Description = "SBOM-Datei", DefaultValueFactory = _ => "build/sbom-maschine.cdx.json" };
        var report = new Option<string>("--report") { Description = "Prüfbericht neben der SBOM", DefaultValueFactory = _ => "html" };
        report.AcceptOnlyFromAmong("html", "md", "none");
        var releaseBuild = new Option<bool>("--release-build") { Description = "Warnungen sind Befunde (Exit 1)" };

        var command = new Command("build", "Liest alle Stationen, führt Hersteller- und Teil-SBOMs zusammen, prüft und schreibt SBOM, Bericht und Ergebnisdatei")
        {
            config, release, commit, output, report, releaseBuild,
        };
        command.SetAction(guard(p => BuildCommand.RunAsync(
            new BuildOptions(p.GetValue(config)!, p.GetValue(release)!, p.GetValue(commit), p.GetValue(output)!, p.GetValue(report)!, p.GetValue(releaseBuild), p.GetValue(verbose)),
            environment.Build,
            stdout)));
        return command;
    }

    private static Command Validate(Func<Func<ParseResult, Task<int>>, Func<ParseResult, CancellationToken, Task<int>>> guard, Option<bool> verbose, TextWriter stdout)
    {
        var sbom = new Argument<string>("sbom") { Description = "CycloneDX-Datei, auch eine fremde" };
        var releaseBuild = new Option<bool>("--release-build") { Description = "Warnungen sind Befunde (Exit 1)" };
        var command = new Command("validate", "Prüft eine SBOM gegen das Schema CycloneDX 1.6 und die Pflichtfelder der BSI TR-03183-2; schreibt <sbom>.result.json") { sbom, releaseBuild };
        command.SetAction(guard(p => ValidateCommand.RunAsync(p.GetValue(sbom)!, p.GetValue(releaseBuild), p.GetValue(verbose), stdout)));
        return command;
    }

    private static Command Report(Func<Func<ParseResult, Task<int>>, Func<ParseResult, CancellationToken, Task<int>>> guard, TextWriter stdout)
    {
        var sbom = new Argument<string>("sbom") { Description = "CycloneDX-Datei" };
        var format = new Option<string>("--format") { Description = "Format des Berichts", Required = true };
        format.AcceptOnlyFromAmong("html", "md");
        var output = new Option<string>("--out") { Description = "Berichtsdatei, z. B. build/sbom-bericht.html", Required = true, HelpName = "datei" };
        var field = new Option<string?>("--field") { Description = "Ist-Stand aus dem Feld; ergänzt den Abschnitt „Abgleich mit dem Feld“", HelpName = "datei" };
        var releaseBuild = new Option<bool>("--release-build") { Description = "Prüfstatus wie im Release-Build: Warnungen gelten als nicht bestanden" };
        var command = new Command("report", "Schreibt den Prüfbericht zu einer SBOM als HTML oder Markdown") { sbom, format, output, field, releaseBuild };
        command.SetAction(guard(p => ReportCommand.RunAsync(
            new ReportOptions(p.GetValue(sbom)!, p.GetValue(format)!, p.GetValue(output)!, p.GetValue(field), p.GetValue(releaseBuild)),
            stdout)));
        return command;
    }

    private static Command Compare(
        Func<Func<ParseResult, Task<int>>, Func<ParseResult, CancellationToken, Task<int>>> guard,
        CliEnvironment environment,
        Option<bool> verbose,
        TextWriter stdout)
    {
        var sbom = new Argument<string>("sbom") { Description = "SBOM mit dem Soll-Stand" };
        var field = new Option<string>("--field") { Description = "Ist-Stand aus dem Feld, z. B. feld/1042.json", Required = true };
        var asBuilt = new Option<string?>("--as-built") { Description = "SBOM für diese Seriennummer mit Ist-Firmware schreiben", HelpName = "datei" };
        var csv = new Option<string?>("--csv") { Description = "Abgleich zusätzlich als CSV", HelpName = "datei" };
        var command = new Command("compare", "Gleicht die SBOM mit einem Ist-Stand aus dem Feld ab; Exit 1 bei Abweichung") { sbom, field, asBuilt, csv };
        command.SetAction(guard(p => CompareCommand.RunAsync(
            new Commands.CompareOptions(p.GetValue(sbom)!, p.GetValue(field)!, p.GetValue(asBuilt), p.GetValue(csv), p.GetValue(verbose)),
            environment.Build,
            stdout)));
        return command;
    }

    private static Command Ids(
        Func<Func<ParseResult, Task<int>>, Func<ParseResult, CancellationToken, Task<int>>> guard,
        Option<bool> verbose,
        TextWriter stdout,
        TextWriter stderr)
    {
        var sbom = new Argument<string>("sbom") { Description = "CycloneDX-Datei" };
        var csaf = new Option<string>("--csaf") { Description = "Verzeichnis mit CSAF-Dokumenten", Required = true, HelpName = "verzeichnis" };
        var cpeMap = new Option<string?>("--cpe-map") { Description = "CPE-Zuordnungstabelle, z. B. kennungen/cpe-map.yml", HelpName = "datei" };
        var output = new Option<string?>("--out") { Description = "Abdeckungsbericht als JSON", HelpName = "datei" };
        var command = new Command("ids", "Prüft, ob die Kennungen der Komponenten in CSAF-Dokumenten oder der CPE-Tabelle auffindbar sind") { sbom, csaf, cpeMap, output };
        command.SetAction(guard(p => IdsCommand.RunAsync(
            new IdsOptions(p.GetValue(sbom)!, p.GetValue(csaf)!, p.GetValue(cpeMap), p.GetValue(output), p.GetValue(verbose)),
            stdout,
            stderr)));
        return command;
    }

    private static Command Inspect(Func<Func<ParseResult, Task<int>>, Func<ParseResult, CancellationToken, Task<int>>> guard, TextWriter stdout, TextWriter stderr)
    {
        var project = new Argument<string>("projektdatei") { Description = "Projektdatei oder Export, z. B. Verpackung.xml (PLCopen XML) oder Linie1.aml" };
        var adapter = new Option<string>("--adapter") { Description = "Adapter", Required = true };
        adapter.AcceptOnlyFromAmong([.. AdapterIds.All]);
        var gsdml = new Option<DirectoryInfo?>("--gsdml") { Description = "Verzeichnis mit GSDML-Dateien" };
        var command = new Command("inspect", "Gibt das Rohinventar eines Adapters als JSON aus") { project, adapter, gsdml };
        command.SetAction(guard(p => InspectCommand.RunAsync(p.GetValue(project)!, p.GetValue(adapter)!, p.GetValue(gsdml)?.ToString(), stdout, stderr)));
        return command;
    }

    private static Command ExportAml(Func<Func<ParseResult, Task<int>>, Func<ParseResult, CancellationToken, Task<int>>> guard, CliEnvironment environment)
    {
        var project = new Option<FileInfo>("--project") { Description = "TIA-Projekt .ap21 oder Archiv .zap21", Required = true };
        var output = new Option<FileInfo>("--out") { Description = "AML-Datei", Required = true };
        var allowUpgrade = new Option<bool>("--allow-upgrade") { Description = "Projekt älterer TIA-Version auf einer Kopie hochrüsten" };
        var command = new Command("export-aml", "Erzeugt den CAx-Export (AML) eines TIA-Projekts über Openness (Windows, TIA Portal V21)") { project, output, allowUpgrade };
        command.SetAction(guard(_ =>
        {
            RequireWindows(environment, "export-aml");
            return NotImplemented("export-aml", "M14");
        }));
        return command;
    }

    private static Command Doctor(Func<Func<ParseResult, Task<int>>, Func<ParseResult, CancellationToken, Task<int>>> guard, CliEnvironment environment)
    {
        var adapter = new Option<string?>("--adapter") { Description = "Adapter, dessen Umgebung geprüft wird" };
        adapter.AcceptOnlyFromAmong(AdapterIds.TiaPortal);
        var deep = new Option<bool>("--deep") { Description = "Lizenz durch Start von TIA Portal ohne Oberfläche prüfen" };
        var command = new Command("doctor", "Prüft die Umgebung des Agenten (Windows, TIA Portal V21)") { adapter, deep };
        command.SetAction(guard(parse =>
        {
            if (parse.GetValue(adapter) == AdapterIds.TiaPortal)
            {
                RequireWindows(environment, "doctor --adapter tia-portal");
            }
            return NotImplemented("doctor", "M14");
        }));
        return command;
    }

    private static void RequireWindows(CliEnvironment environment, string command)
    {
        if (!environment.IsWindows)
        {
            throw new EnvironmentException(
                "OPENNESS_WINDOWS_ONLY",
                $"Der Befehl „{command}“ braucht TIA Portal Openness und läuft nur auf Windows mit TIA Portal V21. Den Schritt auf einem Windows-Agenten mit TIA Portal V21 ausführen.");
        }
    }

    // Skeleton (M0): every command exists with its options, the function follows in the named milestone.
    // Exit 2, see docs/entscheidungen.md E-001.
    private static Task<int> NotImplemented(string command, string milestone) =>
        throw new UsageException(
            "NOT_IMPLEMENTED",
            $"Der Befehl „{command}“ ist in plc-sbom {ToolVersion.Short} noch nicht umgesetzt. Er folgt mit Meilenstein {milestone}.");
}
