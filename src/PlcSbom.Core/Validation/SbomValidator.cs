using System.Text.Json;
using System.Text.Json.Nodes;
using PlcSbom.Core.Gaps;

namespace PlcSbom.Core.Validation;

/// <summary>Result of validating one SBOM: schema status and all findings, sorted.</summary>
public sealed record ValidationReport
{
    public required string SpecVersion { get; init; }

    /// <summary>True if the embedded CycloneDX 1.6 schema accepted the document.</summary>
    public required bool SchemaValid { get; init; }

    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>Number of components including the primary component.</summary>
    public int Components { get; init; }

    /// <summary>Distinct station names (property "&lt;ns&gt;:station").</summary>
    public int Stations { get; init; }

    public int Errors => Findings.Count(f => f.Severity == Severity.Error);

    public int Warnings => Findings.Count(f => f.Severity == Severity.Warning);

    public int Notes => Findings.Count(f => f.Severity == Severity.Note);

    /// <summary>Exit code (CLAUDE.md 7.2): errors always 1, warnings only in a release build.</summary>
    public int ExitCode(bool releaseBuild) =>
        Errors > 0 || (releaseBuild && Warnings > 0) ? ExitCodes.Finding : ExitCodes.Ok;
}

/// <summary>Validator of CycloneDX SBOMs: JSON, schema CycloneDX 1.6, rules of BSI TR-03183-2 v2.1.0.</summary>
public static class SbomValidator
{
    public static async Task<ValidationReport> ValidateAsync(string json)
    {
        JsonObject bom;
        try
        {
            bom = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = false }) as JsonObject
                ?? throw new JsonException("Kein JSON-Objekt.");
        }
        catch (JsonException ex)
        {
            return new ValidationReport
            {
                SpecVersion = "",
                SchemaValid = false,
                Findings = [new(Severity.Error, "JSON_INVALID", $"Die Datei ist kein gültiges JSON: {ex.Message}", Fields: [TrField.SbomFormat])],
            };
        }

        var findings = new List<Finding>();
        var specVersion = bom["specVersion"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        var schemaValid = false;
        if (bom["bomFormat"]?.ToString() != "CycloneDX")
        {
            findings.Add(new(Severity.Error, "NOT_CYCLONEDX", "Die Datei ist keine CycloneDX-SBOM (bomFormat fehlt oder ist nicht „CycloneDX“).", Fields: [TrField.SbomFormat]));
        }
        else if (specVersion == "1.6")
        {
            var errors = await SchemaValidator.ValidateAsync(json).ConfigureAwait(false);
            schemaValid = errors.Count == 0;
            findings.AddRange(errors.Select(e => new Finding(Severity.Error, "SCHEMA_INVALID",
                $"Schema CycloneDX 1.6 verletzt an {e.Path}: {e.Kind}.", Fields: [TrField.SbomFormat])));
        }
        else if (Version.TryParse(specVersion, out var parsed) && parsed > new Version(1, 6))
        {
            findings.Add(new(Severity.Warning, "SCHEMA_NOT_EMBEDDED",
                $"CycloneDX {specVersion} ist nach TR-03183-2 zulässig, das Werkzeug bringt aber nur das Schema 1.6 mit. Die Schemaprüfung entfällt.",
                Fields: [TrField.SbomFormat]));
        }
        else
        {
            findings.Add(new(Severity.Error, "SPEC_VERSION_UNSUPPORTED",
                $"CycloneDX „{specVersion}“ erfüllt TR-03183-2 Abschnitt 4 nicht; verlangt ist mindestens Version 1.6.", Fields: [TrField.SbomFormat]));
        }

        findings.AddRange(TrValidator.Validate(bom));

        return new ValidationReport
        {
            SpecVersion = specVersion,
            SchemaValid = schemaValid,
            Findings = Sort(findings),
            Components = CountComponents(bom),
            Stations = CountStations(bom),
        };
    }

    private static List<Finding> Sort(IEnumerable<Finding> findings) =>
        findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.ComponentRef ?? "", StringComparer.Ordinal)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.Message, StringComparer.Ordinal)
            .ToList();

    private static int CountComponents(JsonObject bom)
    {
        static int Count(JsonArray? array) =>
            (array ?? []).OfType<JsonObject>().Sum(c => 1 + Count(c["components"] as JsonArray));
        return (bom["metadata"]?["component"] is JsonObject ? 1 : 0) + Count(bom["components"] as JsonArray);
    }

    private static int CountStations(JsonObject bom) =>
        (bom["components"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .SelectMany(c => (c["properties"] as JsonArray ?? []).OfType<JsonObject>())
            .Where(p => p["name"]?.ToString() is { } name
                && name.EndsWith(":station", StringComparison.Ordinal)
                && !name.StartsWith("cdx:", StringComparison.Ordinal)
                && !name.StartsWith("bsi:", StringComparison.Ordinal))
            .Select(p => p["value"]?.ToString())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Count();
}
