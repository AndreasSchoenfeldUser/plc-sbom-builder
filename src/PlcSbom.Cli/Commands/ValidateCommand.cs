using PlcSbom.Core;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;

namespace PlcSbom.Cli.Commands;

/// <summary>plc-sbom validate &lt;sbom&gt; (CLAUDE.md section 7.1): schema, TR rules, result file.</summary>
public static class ValidateCommand
{
    public static async Task<int> RunAsync(string sbomPath, bool releaseBuild, bool verbose, TextWriter stdout)
    {
        if (!File.Exists(sbomPath))
        {
            throw new UsageException("SBOM_NOT_FOUND", $"Die SBOM-Datei {sbomPath} gibt es nicht.");
        }
        string json;
        try
        {
            json = await File.ReadAllTextAsync(sbomPath).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new UsageException("SBOM_NOT_READABLE", $"Die SBOM-Datei {sbomPath} ist nicht lesbar: {ex.Message}", ex);
        }

        var report = await SbomValidator.ValidateAsync(json).ConfigureAwait(false);
        var exitCode = report.ExitCode(releaseBuild);
        await ReportWriter.WriteAsync(report, releaseBuild, verbose, stdout).ConfigureAwait(false);

        var resultPath = ResultFile.PathFor(sbomPath);
        await File.WriteAllBytesAsync(resultPath, ResultFile.Write("validate", sbomPath, report, exitCode)).ConfigureAwait(false);
        await stdout.WriteLineAsync($"[ausgabe]   {resultPath}").ConfigureAwait(false);
        return exitCode;
    }
}

/// <summary>Console lines of a validation, as in the UI draft of the product page (CLAUDE.md section 7.1).</summary>
public static class ReportWriter
{
    public static async Task WriteAsync(ValidationReport report, bool releaseBuild, bool verbose, TextWriter stdout)
    {
        var schema = report.SchemaValid
            ? "OK"
            : $"NICHT BESTANDEN, {report.Findings.Count(f => f.Code == "SCHEMA_INVALID")} Fehler";
        if (report.SpecVersion != "1.6" && !report.SchemaValid && report.Findings.All(f => f.Code != "SCHEMA_INVALID"))
        {
            schema = "nicht geprüft";
        }
        await stdout.WriteLineAsync($"[prüfung]   Schema CycloneDX 1.6 ............ {schema}").ConfigureAwait(false);

        var verdict = report.ExitCode(releaseBuild) == ExitCodes.Ok ? "OK" : "NICHT BESTANDEN";
        var counts = new List<string>();
        if (report.Errors > 0)
        {
            counts.Add(Count(report.Errors, "Fehler", "Fehler"));
        }
        counts.Add(Count(report.Warnings, "Warnung", "Warnungen"));
        counts.Add(Count(report.Notes, "Hinweis", "Hinweise"));
        await stdout.WriteLineAsync($"[prüfung]   Pflichtfelder BSI TR-03183-2 .... {verdict}, {string.Join(", ", counts)}").ConfigureAwait(false);

        foreach (var finding in report.Findings.Where(f => verbose || f.Severity != Severity.Note))
        {
            var label = finding.Severity switch
            {
                Severity.Error => "Fehler",
                Severity.Warning => "Warnung",
                _ => "Hinweis",
            };
            await stdout.WriteLineAsync($"            {label}: {Messages.Format(finding.Code, finding.Message)}").ConfigureAwait(false);
        }
        if (!verbose && report.Notes > 0)
        {
            await stdout.WriteLineAsync($"            {Count(report.Notes, "Hinweis", "Hinweise")} ausgeblendet, mit --verbose anzeigen.").ConfigureAwait(false);
        }
    }

    private static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
}
