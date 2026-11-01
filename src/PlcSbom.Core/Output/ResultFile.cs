using System.Text.Encodings.Web;
using System.Text.Json;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Validation;

namespace PlcSbom.Core.Output;

/// <summary>
/// Result file &lt;out&gt;.result.json (CLAUDE.md section 7.3). Lets the pipeline mark a development
/// build with warnings as unstable without overloading the exit code. Field names are an interface.
/// </summary>
public static class ResultFile
{
    public const int FormatVersion = 1;

    /// <summary>build/sbom-maschine.cdx.json → build/sbom-maschine.result.json.</summary>
    public static string PathFor(string sbomPath)
    {
        var directory = Path.GetDirectoryName(sbomPath) ?? "";
        var name = Path.GetFileName(sbomPath);
        foreach (var suffix in new[] { ".cdx.json", ".json" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }
        return Path.Combine(directory, name + ".result.json");
    }

    /// <summary>Result of a run that ended before validation (exit 2, 3 or 4): one error with the code of the cause.</summary>
    public static byte[] WriteAborted(string command, string sbomFile, PlcSbomException cause) =>
        Write(command, sbomFile, new ValidationReport
        {
            SpecVersion = "",
            SchemaValid = false,
            Findings = [new Finding(Severity.Error, cause.Code, cause.Message)],
        }, cause.ExitCode);

    public static byte[] Write(string command, string sbomFile, ValidationReport report, int exitCode)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            w.WriteStartObject();
            w.WriteNumber("format", FormatVersion);
            w.WriteString("command", command);
            w.WriteString("sbom", sbomFile.Replace('\\', '/'));
            w.WriteNumber("exitCode", exitCode);
            w.WriteBoolean("schemaValid", report.SchemaValid);
            w.WriteNumber("errors", report.Errors);
            w.WriteNumber("warnings", report.Warnings);
            w.WriteNumber("notes", report.Notes);
            w.WriteNumber("components", report.Components);
            w.WriteNumber("stations", report.Stations);

            w.WriteStartObject("gaps");
            var gapCodes = GapCatalog.All.ToDictionary(g => TrValidator.ToCode(g.Code), g => g.Code, StringComparer.Ordinal);
            foreach (var group in report.Findings
                .Where(f => gapCodes.ContainsKey(f.Code))
                .GroupBy(f => gapCodes[f.Code])
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                w.WriteNumber(group.Key, group.Count());
            }
            w.WriteEndObject();

            w.WriteStartArray("findings");
            foreach (var f in report.Findings)
            {
                w.WriteStartObject();
                w.WriteString("severity", f.Severity switch
                {
                    Severity.Error => "error",
                    Severity.Warning => "warning",
                    _ => "note",
                });
                w.WriteString("code", f.Code);
                if (f.ComponentRef is not null)
                {
                    w.WriteString("component", f.ComponentRef);
                }
                if (f.Fields.Count > 0)
                {
                    w.WriteStartArray("fields");
                    foreach (var field in f.Fields)
                    {
                        w.WriteStringValue(field);
                    }
                    w.WriteEndArray();
                }
                w.WriteString("message", f.Message);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }
}
