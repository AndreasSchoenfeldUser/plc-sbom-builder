using System.Diagnostics;
using System.Globalization;
using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Build;
using PlcSbom.Core.Config;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;

namespace PlcSbom.Cli.Commands;

/// <summary>Options of plc-sbom build (CLAUDE.md section 7.1).</summary>
public sealed record BuildOptions(string Config, string Release, string? Commit, string Out, string Report, bool ReleaseBuild, bool Verbose);

/// <summary>
/// plc-sbom build: reads all stations, merges vendor and partial SBOMs, validates, writes the SBOM and
/// the result file. One call in the pipeline, one exit code (CLAUDE.md sections 7.1 to 7.3).
/// </summary>
public static class BuildCommand
{
    public static async Task<int> RunAsync(BuildOptions options, IBuildEnvironment environment, TextWriter stdout)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var config = MachineConfigLoader.Load(options.Config);
            var outcome = await MachineBuilder.BuildAsync(new BuildRequest { Config = config, Release = options.Release, Commit = options.Commit }, environment).ConfigureAwait(false);
            foreach (var station in outcome.Stations)
            {
                await stdout.WriteLineAsync($"{$"[adapter {station.Adapter}]",-21} {StationLine(station)}").ConfigureAwait(false);
            }
            await stdout.WriteLineAsync($"[quellen]   {SourcesLine(outcome)}").ConfigureAwait(false);

            var sbom = CycloneDxWriter.Write(outcome.Machine, new WriterOptions { Namespace = config.Namespace, Timestamp = outcome.Timestamp });
            var report = (await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(sbom)).ConfigureAwait(false)).WithFindings(outcome.Findings);
            await stdout.WriteLineAsync($"[merge]     {outcome.Machine.Name} > {Count(report.Stations, "Station", "Stationen")} > {Count(report.Components, "Komponente", "Komponenten")}").ConfigureAwait(false);

            var exitCode = report.ExitCode(options.ReleaseBuild);
            await ReportWriter.WriteAsync(report, options.ReleaseBuild, options.Verbose, stdout).ConfigureAwait(false);
            if (options.Report != "none")
            {
                await stdout.WriteLineAsync("[bericht]   Der Prüfbericht folgt mit Meilenstein M10; in dieser Version wird keiner geschrieben.").ConfigureAwait(false);
            }

            WriteFile(options.Out, sbom);
            var resultPath = ResultFile.PathFor(options.Out);
            WriteFile(resultPath, ResultFile.Write("build", options.Out, report, exitCode));
            await stdout.WriteLineAsync($"[ausgabe]   {options.Out}").ConfigureAwait(false);
            await stdout.WriteLineAsync($"[ausgabe]   {resultPath}").ConfigureAwait(false);
            await stdout.WriteLineAsync($"Fertig in {clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} s").ConfigureAwait(false);
            return exitCode;
        }
        catch (PlcSbomException ex)
        {
            // The pipeline reads the result file also after exit 2, 3 or 4 (CLAUDE.md section 10).
            TryWriteAborted(options.Out, ex);
            throw;
        }
    }

    private static string StationLine(StationSummary station)
    {
        var details = new List<string>();
        if (station.ExportedAt is { } exported)
        {
            details.Add($"Export {exported.ToUniversalTime():dd.MM.yyyy}");
        }
        if (station.Tool is { } tool)
        {
            details.Add($"{tool.Name} {tool.Version}");
        }
        var suffix = details.Count == 0 ? "" : $" ({string.Join(", ", details)})";
        return $"Station {station.Name}: {Count(station.Devices, "Gerät", "Geräte")}, {Count(station.Libraries, "Bibliothek", "Bibliotheken")}{suffix}";
    }

    private static string SourcesLine(BuildOutcome outcome)
    {
        var vendor = Count(outcome.VendorSboms, "Hersteller-SBOM", "Hersteller-SBOMs");
        if (outcome.VendorSboms > 0)
        {
            vendor += $" ({outcome.VendorSbomsUsed} zugeordnet)";
        }
        var partial = Count(outcome.PartialSbomTools.Count, "Teil-SBOM", "Teil-SBOMs");
        var tools = outcome.PartialSbomTools.OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (tools.Count > 0)
        {
            partial += $" ({string.Join(", ", tools)})";
        }
        return $"{vendor}, {partial}, {Count(outcome.GsdmlFiles, "GSDML-Datei", "GSDML-Dateien")}";
    }

    private static void WriteFile(string path, byte[] content)
    {
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } directory)
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(path, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UsageException("OUTPUT_NOT_WRITABLE", $"Die Datei {path} lässt sich nicht schreiben: {ex.Message}", ex);
        }
    }

    private static void TryWriteAborted(string sbomPath, PlcSbomException cause)
    {
        try
        {
            var path = ResultFile.PathFor(sbomPath);
            if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } directory)
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(path, ResultFile.WriteAborted("build", sbomPath, cause));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The exit code still tells the pipeline what happened.
        }
    }

    private static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
}
