using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Build;
using PlcSbom.Core.FieldState;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;

namespace PlcSbom.Cli.Commands;

/// <summary>Options of plc-sbom compare (CLAUDE.md section 7.1).</summary>
public sealed record CompareOptions(string Sbom, string Field, string? AsBuilt, string? Csv, bool Verbose);

/// <summary>
/// plc-sbom compare &lt;sbom&gt; --field &lt;datei&gt;: compares the projected state with an as-found state
/// (CLAUDE.md section 8.3). Every deviation is exit 1. --as-built writes the SBOM of this machine with the
/// firmware found in the field, also when devices deviate; --csv writes the comparison as CSV. The result
/// file goes next to the SBOM as &lt;sbom&gt;.compare-&lt;serial&gt;.result.json (E-023).
/// </summary>
public static class CompareCommand
{
    public static async Task<int> RunAsync(CompareOptions options, IBuildEnvironment environment, TextWriter stdout)
    {
        var (_, sbom) = await CommandFiles.LoadSbomAsync(options.Sbom).ConfigureAwait(false);
        var field = FieldStateReader.Load(options.Field);
        var comparison = FieldComparer.Compare(sbom, field);

        var devices = sbom.Components.Count(c => c.IsDevice);
        await stdout.WriteLineAsync($"[sbom]      {options.Sbom}: {sbom.Primary?.Name} {sbom.Primary?.Version}, {CommandFiles.Count(devices, "Gerät", "Geräte")}").ConfigureAwait(false);
        await stdout.WriteLineAsync($"[feld]      {Describe(field)}, {CommandFiles.Count(field.Devices.Count, "Gerät", "Geräte")}").ConfigureAwait(false);
        foreach (var entry in comparison.Entries.Where(e => options.Verbose || e.Status is not (ComparisonStatus.Equal or ComparisonStatus.Passive)))
        {
            await stdout.WriteLineAsync($"[abgleich]  {entry.StatusText,-15} {Line(entry)}").ConfigureAwait(false);
        }
        await stdout.WriteLineAsync($"[abgleich]  {Summary(comparison)}").ConfigureAwait(false);

        var findings = new List<Finding>();
        foreach (var warning in field.Warnings)
        {
            findings.Add(new Finding(Severity.Warning, warning.Code, warning.Message));
        }

        var report = new ValidationReport { SpecVersion = sbom.SpecVersion ?? "", SchemaValid = true, Findings = [] };
        if (options.AsBuilt is { } asBuiltPath)
        {
            var timestamp = CommandFiles.SourceDateEpoch(environment.Variable("SOURCE_DATE_EPOCH"))
                ?? new DateTimeOffset(field.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var asBuilt = AsBuiltWriter.Write(sbom, comparison, timestamp);
            findings.AddRange(asBuilt.Warnings.Select(w => new Finding(Severity.Warning, w.Code, w.Message)));
            report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(asBuilt.Sbom)).ConfigureAwait(false);
            CommandFiles.Write(asBuiltPath, asBuilt.Sbom);
            await stdout.WriteLineAsync($"[as-built]  Seriennummer {field.SerialNumber}: {CommandFiles.Count(asBuilt.FirmwareChanged, "Firmware-Stand", "Firmware-Stände")} aus dem Feld übernommen").ConfigureAwait(false);
            await stdout.WriteLineAsync(
                $"[prüfung]   as-built-SBOM: Schema CycloneDX 1.6 {(report.SchemaValid ? "OK" : "NICHT BESTANDEN")}, "
                + $"{CommandFiles.Count(report.Errors, "Fehler", "Fehler")}, {CommandFiles.Count(report.Warnings, "Warnung", "Warnungen")}, {CommandFiles.Count(report.Notes, "Hinweis", "Hinweise")}").ConfigureAwait(false);
            foreach (var finding in report.Findings.Where(f => f.Severity == Severity.Error || (options.Verbose && f.Severity == Severity.Warning)))
            {
                await stdout.WriteLineAsync($"            {(finding.Severity == Severity.Error ? "Fehler" : "Warnung")}: {Messages.Format(finding.Code, finding.Message)}").ConfigureAwait(false);
            }
        }
        report = report.WithFindings(findings);
        foreach (var finding in findings)
        {
            await stdout.WriteLineAsync($"            Warnung: {Messages.Format(finding.Code, finding.Message)}").ConfigureAwait(false);
        }

        var exitCode = comparison.Deviations > 0 || report.Errors > 0 ? ExitCodes.Finding : ExitCodes.Ok;
        if (options.Csv is { } csv)
        {
            CommandFiles.Write(csv, ComparisonCsv.Write(comparison));
            await stdout.WriteLineAsync($"[ausgabe]   {csv}").ConfigureAwait(false);
        }
        if (options.AsBuilt is not null)
        {
            await stdout.WriteLineAsync($"[ausgabe]   {options.AsBuilt}").ConfigureAwait(false);
        }
        var resultPath = ResultPathFor(options.Sbom, field.SerialNumber);
        CommandFiles.Write(resultPath, ResultFile.Write("compare", options.Sbom, report, exitCode, comparison, options.AsBuilt));
        await stdout.WriteLineAsync($"[ausgabe]   {resultPath}").ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>build/sbom-maschine.cdx.json, serial 1042 → build/sbom-maschine.compare-1042.result.json.</summary>
    public static string ResultPathFor(string sbomPath, string serialNumber) =>
        Path.Combine(Path.GetDirectoryName(sbomPath) ?? "", $"{CommandFiles.Stem(sbomPath)}.compare-{BomRef.Slug(serialNumber)}.result.json");

    public static string Summary(Comparison c) =>
        $"{c.Checked} geprüft: {c.Count(ComparisonStatus.Equal)} gleich, {c.Count(ComparisonStatus.Different)} abweichend, "
        + $"{c.Count(ComparisonStatus.ProjectOnly)} nur im Projekt, {c.Count(ComparisonStatus.FieldOnly)} nur im Feld, {c.Count(ComparisonStatus.Passive)} passiv";

    private static string Describe(AsFoundState field) =>
        $"{field.SourcePath}: {field.Machine}, Seriennummer {field.SerialNumber}, {field.Date:dd.MM.yyyy}{(field.Source is null ? "" : $" ({field.Source})")}";

    private static string Line(ComparisonEntry e)
    {
        var where = string.Join(" ", new[] { e.Station, e.Position }.OfType<string>());
        var text = $"{where} {e.Device}".Trim();
        return e.Notes.Count == 0 ? text : $"{text}: {string.Join(" ", e.Notes)}";
    }
}
