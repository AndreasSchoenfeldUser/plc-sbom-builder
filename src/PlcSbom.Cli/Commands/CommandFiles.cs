using System.Text.Json;
using PlcSbom.Core;
using PlcSbom.Core.Output.Report;
using PlcSbom.Core.Sbom;

namespace PlcSbom.Cli.Commands;

/// <summary>Reading the SBOM and writing output files, shared by build, report, compare and ids.</summary>
internal static class CommandFiles
{
    /// <summary>SBOM text and view. Missing, unreadable or no JSON object: exit 2.</summary>
    public static async Task<(string Json, SbomView View)> LoadSbomAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new UsageException("SBOM_NOT_FOUND", $"Die SBOM-Datei {path} gibt es nicht.");
        }
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UsageException("SBOM_NOT_READABLE", $"Die SBOM-Datei {path} ist nicht lesbar: {ex.Message}", ex);
        }
        try
        {
            return (json, SbomView.Parse(json));
        }
        catch (JsonException ex)
        {
            throw new UsageException("SBOM_INVALID", $"Die SBOM-Datei {path} ist kein gültiges JSON-Objekt: {ex.Message}", ex);
        }
    }

    /// <summary>Writes a file, creating its directory. Not writable: exit 2.</summary>
    public static void Write(string path, byte[] content)
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

    /// <summary>build/sbom-maschine.cdx.json → build/sbom-maschine.html (or .md).</summary>
    public static string ReportPathFor(string sbomPath, string format)
    {
        var directory = Path.GetDirectoryName(sbomPath) ?? "";
        return Path.Combine(directory, Stem(sbomPath) + (format == "md" ? ".md" : ".html"));
    }

    /// <summary>File name without .cdx.json or .json.</summary>
    public static string Stem(string sbomPath)
    {
        var name = Path.GetFileName(sbomPath);
        foreach (var suffix in new[] { ".cdx.json", ".json" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^suffix.Length];
            }
        }
        return name;
    }

    public static byte[] Render(ReportDocument report, string format) =>
        format == "md" ? MarkdownReport.Write(report) : HtmlReport.Write(report);

    /// <summary>SOURCE_DATE_EPOCH as time, null if not set; not a number of seconds: exit 2.</summary>
    public static DateTimeOffset? SourceDateEpoch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return long.TryParse(value.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds <= 253402300799
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : throw new UsageException("SOURCE_DATE_EPOCH_INVALID", $"SOURCE_DATE_EPOCH „{value}“ ist keine Zahl von Sekunden seit 1970-01-01 UTC.");
    }

    public static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
}
