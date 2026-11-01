using PlcSbom.Core.FieldState;
using PlcSbom.Core.Output.Report;
using PlcSbom.Core.Validation;

namespace PlcSbom.Cli.Commands;

/// <summary>Options of plc-sbom report (CLAUDE.md section 7.1).</summary>
public sealed record ReportOptions(string Sbom, string Format, string Out, string? Field, bool ReleaseBuild);

/// <summary>
/// plc-sbom report &lt;sbom&gt;: Prüfbericht from an existing SBOM (CLAUDE.md section 8.2). Validates the SBOM
/// for the status and the gaps, optionally adds the comparison with a field file. The hardware appendix
/// needs the projects and is only in the report of build (E-022). Exit 0 when the report is written,
/// whatever the SBOM contains; the gate is build or validate.
/// </summary>
public static class ReportCommand
{
    public static async Task<int> RunAsync(ReportOptions options, TextWriter stdout)
    {
        var (json, sbom) = await CommandFiles.LoadSbomAsync(options.Sbom).ConfigureAwait(false);
        var validation = await SbomValidator.ValidateAsync(json).ConfigureAwait(false);
        var comparison = options.Field is null ? null : FieldComparer.Compare(sbom, FieldStateReader.Load(options.Field));
        var report = ReportBuilder.Build(new ReportInput
        {
            Sbom = sbom,
            SbomPath = options.Sbom.Replace('\\', '/'),
            Validation = validation,
            ReleaseBuild = options.ReleaseBuild,
            Comparison = comparison,
        });
        CommandFiles.Write(options.Out, CommandFiles.Render(report, options.Format));
        await stdout.WriteLineAsync($"[bericht]   {options.Out} ({(options.Format == "md" ? "Markdown" : "HTML")}), Prüfung {report.Verdict}").ConfigureAwait(false);
        if (comparison is not null)
        {
            await stdout.WriteLineAsync($"[abgleich]  {CompareCommand.Summary(comparison)}").ConfigureAwait(false);
        }
        return 0;
    }
}
