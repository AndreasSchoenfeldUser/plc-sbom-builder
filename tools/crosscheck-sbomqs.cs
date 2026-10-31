// Cross-check of the own TR-03183-2 validator against sbomqs (Interlynk), CLAUDE.md section 9, ADR E-010.
//
//   dotnet run tools/crosscheck-sbomqs.cs                                   all golden files
//   dotnet run tools/crosscheck-sbomqs.cs -- --sbomqs ./sbomqs a.cdx.json   given files, sbomqs binary
//   dotnet run tools/crosscheck-sbomqs.cs -- --markdown bericht.md          additionally write a Markdown report
//
// Run from the repository root. Without file arguments it checks tests/PlcSbom.Tests/golden/*.cdx.json.
// Exit 0 = both agree, 1 = difference, 2 = call, 3 = sbomqs missing or failed, 4 = unexpected.
//
// sbomqs gives scores, not a verdict, and knows neither gap records nor logical components (TR 3.2.2).
// So the comparison is per component and required TR field:
//   sbomqs: present  + own validator: no error            → agree (present)
//   sbomqs: missing  + own validator: error                → agree (missing)
//   sbomqs: missing  + own validator: recorded gap/logical → explained
//   anything else                                          → difference, exit 1
// Fields TR 5.2.3/5.2.4 require only "if it exists" and the optional ones of 5.2.5 are not compared.

#:project ../src/PlcSbom.Core/PlcSbom.Core.csproj

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Validation;

const string PinnedVersion = "v2.1.3";
const string GoldenDir = "tests/PlcSbom.Tests/golden";

// sbomqs data field (compliance engine 1, --bsi-v21) → TR field of the own validator.
var componentFields = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["component creator"] = TrField.Creator,
    ["component name"] = TrField.Name,
    ["component version"] = TrField.Version,
    ["filename"] = TrField.Filename,
    ["dependencies"] = TrField.Dependencies,
    ["distribution licence (concluded)"] = TrField.DistributionLicence,
    ["hash of deployable component"] = TrField.Hash,
    ["executable property"] = TrField.Executable,
    ["archive property"] = TrField.Archive,
    ["structured property"] = TrField.Structured,
};
var documentFields = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["vuln"] = TrField.SbomVulnerabilities,
    ["specification version"] = TrField.SbomFormat,
    ["creator of SBOM"] = TrField.SbomCreator,
    ["timestamp"] = TrField.SbomTimestamp,
    ["dependency graph completeness"] = TrField.DependencyCompleteness,
};
// TR 5.2.3/5.2.4: MUST only "if it exists"; 5.2.5 and 8.1.16: optional; "components": count only.
var notCompared = new HashSet<string>(StringComparer.Ordinal)
{
    "SBOM-URI", "source code URI", "URI of deployable form", "other unique identifiers",
    "original licence (declared)", "effective licence", "hash of source code", "security.txt URL",
    "BOM links", "components",
};

var sbomqs = "sbomqs";
string? markdownPath = null;
var files = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--sbomqs" when i + 1 < args.Length:
            sbomqs = args[++i];
            break;
        case "--markdown" when i + 1 < args.Length:
            markdownPath = args[++i];
            break;
        case var a when a.StartsWith("--", StringComparison.Ordinal):
            Console.Error.WriteLine("Aufruf: dotnet run tools/crosscheck-sbomqs.cs [-- [--sbomqs <pfad>] [--markdown <datei>] <sbom>...]");
            return 2;
        default:
            files.Add(args[i]);
            break;
    }
}
if (files.Count == 0)
{
    if (!Directory.Exists(GoldenDir))
    {
        Console.Error.WriteLine($"{GoldenDir} nicht gefunden. Aus dem Wurzelverzeichnis des Repositorys aufrufen oder Dateien angeben.");
        return 2;
    }
    files.AddRange(Directory.GetFiles(GoldenDir, "*.cdx.json").Select(f => f.Replace('\\', '/')).Order(StringComparer.Ordinal));
}

var rows = new List<Row>();
var problems = new List<string>();
var sbomqsVersion = "";
foreach (var file in files)
{
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"[SBOM_NOT_FOUND] Die Datei {file} gibt es nicht.");
        return 2;
    }

    string sbomqsJson;
    try
    {
        sbomqsJson = Run(sbomqs, "compliance", "--bsi-v21", "--json", file);
    }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
    {
        Console.Error.WriteLine($"[SBOMQS_UNAVAILABLE] sbomqs konnte nicht ausgeführt werden ({sbomqs}): {ex.Message}");
        return 3;
    }
    var external = JsonNode.Parse(sbomqsJson)!.AsObject();
    sbomqsVersion = external["tool"]?["version"]?.GetValue<string>() ?? "";

    var json = File.ReadAllText(file);
    var report = await SbomValidator.ValidateAsync(json);
    var bom = JsonNode.Parse(json)!.AsObject();
    // Schema violations carry no component reference; they are matched only where sbomqs also misses a value.
    var schemaInvalid = report.Findings.Any(f => f.Code == "SCHEMA_INVALID");

    // sbomqs names components "<name>-<version>", shortened (common.UniqueElementID).
    var refsById = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (var component in Components(bom))
    {
        var id = ElementId(Text(component["name"]) ?? "", Text(component["version"]) ?? "");
        var bomRef = Text(component["bom-ref"]) ?? "";
        if (!refsById.TryGetValue(id, out var list))
        {
            refsById[id] = list = [];
        }
        list.Add(bomRef);
    }

    foreach (var record in (external["sections"] as JsonArray ?? []).OfType<JsonObject>())
    {
        var field = Text(record["section_data_field"]) ?? "";
        var element = Text(record["element_id"]) ?? "";
        var present = record["score"]!.GetValue<double>() >= 10.0;
        var result = Text(record["element_result"]) ?? "";
        var sbomqsText = present ? "vorhanden" : string.IsNullOrEmpty(result) ? "fehlt" : $"fehlt ({result})";

        if (notCompared.Contains(field))
        {
            continue;
        }
        if (documentFields.TryGetValue(field, out var docField))
        {
            // sbomqs judges the whole graph (reachability, completeness indication); the own validator
            // reports this per component.
            string[] graphFields = [TrField.Dependencies, TrField.DependencyCompleteness, TrField.DependencyGraph];
            var own = docField == TrField.DependencyCompleteness
                ? report.Findings.Where(f => f.Fields.Any(graphFields.Contains)).ToList()
                : report.Findings.Where(f => f.ComponentRef is null && f.Fields.Contains(docField)).ToList();
            rows.Add(Classify(file, "SBOM", field, present, sbomqsText, own, schemaInvalid));
            continue;
        }
        if (!componentFields.TryGetValue(field, out var trField))
        {
            problems.Add($"{file}: sbomqs meldet das Feld „{field}“, das der Abgleich nicht kennt. Zuordnung in tools/crosscheck-sbomqs.cs ergänzen.");
            continue;
        }
        if (!refsById.TryGetValue(element, out var refs))
        {
            problems.Add($"{file}: Die sbomqs-Kennung „{element}“ passt zu keiner Komponente.");
            continue;
        }
        // Identical modules (same name and version, e.g. two 1756-EN2T 11.1) share one sbomqs id; which of
        // them a record belongs to is unknown. The record counts only if the validator judges all of them alike.
        var candidates = refs
            .Select(r => Classify(file, r, field, present, sbomqsText,
                report.Findings.Where(f => f.ComponentRef == r && f.Fields.Contains(trField)).ToList(), schemaInvalid))
            .ToList();
        if (candidates.Select(c => (c.Verdict, c.Own)).Distinct().Count() > 1)
        {
            problems.Add($"{file}: Die sbomqs-Kennung „{element}“ passt zu mehreren Komponenten ({string.Join(", ", refs)}), und der Validator bewertet „{field}“ dort unterschiedlich; Abgleich nicht eindeutig.");
            continue;
        }
        rows.Add(candidates[0] with { Element = string.Join(", ", refs) });
    }
}

var differences = rows.Where(r => r.Verdict == Verdict.Difference).ToList();
var output = Render(rows, problems, sbomqsVersion);
Console.Write(output);
if (markdownPath is not null)
{
    File.WriteAllText(markdownPath, output, new UTF8Encoding(false));
}
if (sbomqsVersion != PinnedVersion)
{
    Console.WriteLine($"Hinweis: sbomqs {sbomqsVersion} statt der geprüften Version {PinnedVersion}. Feldnamen und Kennungen können abweichen.");
}
return differences.Count > 0 || problems.Count > 0 ? 1 : 0;

static Row Classify(string file, string element, string field, bool present, string sbomqsText, List<Finding> findings, bool schemaInvalid)
{
    var errors = findings.Where(f => f.Severity == Severity.Error && f.Code != "SCHEMA_INVALID").Select(f => f.Code).Distinct().Order(StringComparer.Ordinal).ToList();
    var explained = findings.Where(f => f.Severity != Severity.Error).Select(f => f.Code).Distinct().Order(StringComparer.Ordinal).ToList();
    if (!present && errors.Count == 0 && explained.Count == 0 && schemaInvalid)
    {
        errors.Add("SCHEMA_INVALID");
    }
    if (present)
    {
        return errors.Count == 0
            ? new Row(file, element, field, sbomqsText, "vorhanden", Verdict.AgreePresent)
            : new Row(file, element, field, sbomqsText, "Fehler " + string.Join(", ", errors), Verdict.Difference);
    }
    if (errors.Count > 0)
    {
        return new Row(file, element, field, sbomqsText, "Fehler " + string.Join(", ", errors), Verdict.AgreeMissing);
    }
    return explained.Count > 0
        ? new Row(file, element, field, sbomqsText, string.Join(", ", explained), Verdict.Explained)
        : new Row(file, element, field, sbomqsText, "keine Meldung", Verdict.Difference);
}

static string Render(List<Row> rows, List<string> problems, string version)
{
    var s = new StringBuilder();
    s.Append("# Gegenprüfung mit sbomqs\n\n");
    s.Append($"sbomqs {version}, `compliance --bsi-v21`. Verglichen werden die Pflichtfelder aus TR-03183-2 Abschnitt 3.1, 4, 5.2.1 und 5.2.2.\n\n");
    s.Append("| Datei | gleich, vorhanden | gleich, fehlt | erklärt | Abweichung |\n|---|---:|---:|---:|---:|\n");
    foreach (var group in rows.GroupBy(r => r.File))
    {
        s.Append($"| `{Path.GetFileName(group.Key)}` | {Count(group, Verdict.AgreePresent)} | {Count(group, Verdict.AgreeMissing)} | {Count(group, Verdict.Explained)} | {Count(group, Verdict.Difference)} |\n");
    }

    var explained = rows.Where(r => r.Verdict == Verdict.Explained)
        .GroupBy(r => (r.Field, r.Own))
        .OrderBy(g => g.Key.Field, StringComparer.Ordinal)
        .ThenBy(g => g.Key.Own, StringComparer.Ordinal)
        .ToList();
    if (explained.Count > 0)
    {
        s.Append("\n## Erklärt: sbomqs vermisst das Feld, der Validator führt einen Lückenvermerk\n\n");
        s.Append("| Feld (sbomqs) | Meldung des Validators | Komponenten |\n|---|---|---|\n");
        foreach (var g in explained)
        {
            s.Append($"| {g.Key.Field} | {g.Key.Own} | {string.Join(", ", g.Select(r => r.Element).Distinct().Order(StringComparer.Ordinal))} |\n");
        }
    }

    var differences = rows.Where(r => r.Verdict == Verdict.Difference).ToList();
    if (differences.Count > 0 || problems.Count > 0)
    {
        s.Append("\n## Abweichungen\n\nNach CLAUDE.md Abschnitt 9 gilt jede Abweichung als Fehler im eigenen Validator, bis das Gegenteil belegt ist.\n\n");
        s.Append("| Datei | Komponente | Feld (sbomqs) | sbomqs | Validator |\n|---|---|---|---|---|\n");
        foreach (var r in differences)
        {
            s.Append($"| `{Path.GetFileName(r.File)}` | {r.Element} | {r.Field} | {r.Sbomqs} | {r.Own} |\n");
        }
        foreach (var p in problems)
        {
            s.Append($"\n- {p}");
        }
        s.Append('\n');
    }
    else
    {
        s.Append("\nKeine Abweichung.\n");
    }
    return s.ToString();
}

static int Count(IEnumerable<Row> rows, Verdict verdict) => rows.Count(r => r.Verdict == verdict);

static IEnumerable<JsonObject> Components(JsonObject bom)
{
    if (bom["metadata"]?["component"] is JsonObject primary)
    {
        yield return primary;
    }
    var stack = new Stack<JsonObject>((bom["components"] as JsonArray ?? []).OfType<JsonObject>().Reverse());
    while (stack.Count > 0)
    {
        var component = stack.Pop();
        yield return component;
        foreach (var child in (component["components"] as JsonArray ?? []).OfType<JsonObject>().Reverse())
        {
            stack.Push(child);
        }
    }
}

// Same as sbomqs pkg/compliance/common.UniqueElementID (v2.1.3): Go path.Base of the name, then the
// name cut to 12 + "..." + 8 bytes above 20 bytes and the version to 6 + "..." + 6 bytes above 12 bytes.
static string ElementId(string name, string version)
{
    return Shorten(GoPathBase(name), 20, 12, 8) + "-" + Shorten(version, 12, 6, 6);

    static string GoPathBase(string path)
    {
        if (path.Length == 0)
        {
            return ".";
        }
        path = path.TrimEnd('/');
        if (path.Length == 0)
        {
            return "/";
        }
        var slash = path.LastIndexOf('/');
        return slash >= 0 ? path[(slash + 1)..] : path;
    }

    static string Shorten(string text, int max, int head, int tail)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return bytes.Length <= max
            ? text
            : Encoding.UTF8.GetString([.. bytes[..head], .. "..."u8, .. bytes[^tail..]]);
    }
}

static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

static string Run(string file, params string[] arguments)
{
    var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments)
    {
        info.ArgumentList.Add(argument);
    }
    using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} startet nicht.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Exit {process.ExitCode}: {stderr.Trim()}");
    }
    return stdout.Result;
}

enum Verdict { AgreePresent, AgreeMissing, Explained, Difference }

sealed record Row(string File, string Element, string Field, string Sbomqs, string Own, Verdict Verdict);
