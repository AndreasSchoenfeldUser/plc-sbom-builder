using System.Text.Encodings.Web;
using System.Text.Json;
using PlcSbom.Core.FieldState;
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

    public static byte[] Write(string command, string sbomFile, ValidationReport report, int exitCode, Comparison? comparison = null, string? asBuilt = null)
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
            if (comparison is not null)
            {
                WriteComparison(w, comparison, asBuilt);
            }

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

    /// <summary>compare: key figures and every device that is not "gleich" (CLAUDE.md section 7.3).</summary>
    private static void WriteComparison(Utf8JsonWriter w, Comparison comparison, string? asBuilt)
    {
        w.WriteStartObject("comparison");
        w.WriteString("field", comparison.Field.SourcePath);
        w.WriteString("fieldSha256", comparison.Field.Sha256);
        w.WriteString("serialNumber", comparison.Field.SerialNumber);
        if (asBuilt is not null)
        {
            w.WriteString("asBuilt", asBuilt.Replace('\\', '/'));
        }
        w.WriteNumber("checked", comparison.Checked);
        w.WriteNumber("equal", comparison.Count(ComparisonStatus.Equal));
        w.WriteNumber("different", comparison.Count(ComparisonStatus.Different));
        w.WriteNumber("projectOnly", comparison.Count(ComparisonStatus.ProjectOnly));
        w.WriteNumber("fieldOnly", comparison.Count(ComparisonStatus.FieldOnly));
        w.WriteNumber("passive", comparison.Count(ComparisonStatus.Passive));
        w.WriteNumber("deviations", comparison.Deviations);
        w.WriteStartArray("deviationList");
        foreach (var e in comparison.Entries.Where(e => e.Status is ComparisonStatus.Different or ComparisonStatus.ProjectOnly or ComparisonStatus.FieldOnly))
        {
            w.WriteStartObject();
            w.WriteString("status", e.StatusText);
            Optional(w, "component", e.Sbom?.BomRef);
            Optional(w, "station", e.Station);
            Optional(w, "position", e.Position);
            Optional(w, "orderNumberSbom", e.SbomOrderNumber);
            Optional(w, "orderNumberField", e.Field?.OrderNumber);
            Optional(w, "firmwareSbom", e.SbomFirmware);
            Optional(w, "firmwareField", e.Field?.Firmware);
            w.WriteString("message", string.Join(" ", e.Notes));
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Optional(Utf8JsonWriter w, string name, string? value)
    {
        if (value is not null)
        {
            w.WriteString(name, value);
        }
    }
}
