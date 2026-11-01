using PlcSbom.Core;
using PlcSbom.Core.Identifiers;

namespace PlcSbom.Cli.Commands;

/// <summary>Options of plc-sbom ids (CLAUDE.md section 7.1).</summary>
public sealed record IdsOptions(string Sbom, string Csaf, string? CpeMap, string? Out, bool Verbose);

/// <summary>
/// plc-sbom ids &lt;sbom&gt; --csaf &lt;verzeichnis&gt;: coverage of the identifiers against local CSAF documents
/// and the CPE table (CLAUDE.md section 6 item 5, H4). A measurement, not a gate: exit 0 whatever the
/// coverage; exit 2 if SBOM, directory or table are missing or the directory holds no CSAF document.
/// </summary>
public static class IdsCommand
{
    public static async Task<int> RunAsync(IdsOptions options, TextWriter stdout, TextWriter stderr)
    {
        var (_, sbom) = await CommandFiles.LoadSbomAsync(options.Sbom).ConfigureAwait(false);
        var csaf = CsafIndex.Load(options.Csaf);
        foreach (var (code, message) in csaf.Warnings)
        {
            await stderr.WriteLineAsync(Messages.Format(code, message)).ConfigureAwait(false);
        }
        if (csaf.Documents.Count == 0)
        {
            throw new UsageException("CSAF_NONE", $"Im Verzeichnis {options.Csaf} liegt kein lesbares CSAF-Dokument (*.json mit document.csaf_version).");
        }
        var cpeMap = options.CpeMap is null ? null : CpeMap.Load(options.CpeMap, options.CpeMap.Replace('\\', '/'));
        var report = IdsCheck.Run(sbom, csaf, cpeMap);

        await stdout.WriteLineAsync(
            $"[csaf]      {options.Csaf}: {CommandFiles.Count(csaf.Documents.Count, "Dokument", "Dokumente")}, "
            + $"{csaf.Products.Count(p => p.ModelNumbers.Count > 0)} Produkte mit model_numbers, {csaf.Products.Count(p => p.Cpe is not null)} mit CPE").ConfigureAwait(false);
        if (cpeMap is not null)
        {
            await stdout.WriteLineAsync($"[cpe]       {options.CpeMap}: {CommandFiles.Count(cpeMap.Entries.Count, "Eintrag", "Einträge")}").ConfigureAwait(false);
        }
        foreach (var entry in report.Entries.Where(e => options.Verbose || e.Status != IdsStatus.None))
        {
            await stdout.WriteLineAsync($"[kennung]   {entry.StatusText,-15} {Line(entry)}").ConfigureAwait(false);
        }
        var share = report.WithIdentifier == 0 ? "" : $" ({100 * report.Count(IdsStatus.Found) / report.WithIdentifier} %)";
        await stdout.WriteLineAsync(
            $"[abdeckung] {report.Count(IdsStatus.Found)} von {report.WithIdentifier} Komponenten mit Kennung in CSAF gefunden{share}; "
            + $"{report.Count(IdsStatus.Pattern)} mit Bestellnummer als Muster, {report.Count(IdsStatus.None)} ohne Kennung").ConfigureAwait(false);
        if (options.Out is { } output)
        {
            CommandFiles.Write(output, IdsCheck.WriteJson(report, options.Sbom));
            await stdout.WriteLineAsync($"[ausgabe]   {output}").ConfigureAwait(false);
        }
        return ExitCodes.Ok;
    }

    private static string Line(IdsEntry e)
    {
        var id = e.OrderNumber ?? e.Cpe ?? "";
        var text = $"{e.Component.BomRef} ({e.Component.Name}) {id}".TrimEnd();
        if (e.Hits.Count > 0)
        {
            var documents = e.Hits.Select(h => h.Document).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            text += $": {string.Join(", ", documents)}";
            if (e.Hits.Any(h => h.Partial))
            {
                text += " (auch über abgekürzte Modellnummern)";
            }
        }
        if (e.InCpeMap is { } inMap)
        {
            text += inMap ? "; CPE-Tabelle: Eintrag vorhanden" : "; CPE-Tabelle: kein Eintrag";
        }
        return text;
    }
}
