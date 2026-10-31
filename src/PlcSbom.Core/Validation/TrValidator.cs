using System.Text.Json.Nodes;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Output;

namespace PlcSbom.Core.Validation;

/// <summary>One finding of the validator. <see cref="Fields"/> names the TR data fields it concerns.</summary>
public sealed record Finding(
    Severity Severity,
    string Code,
    string Message,
    string? ComponentRef = null,
    string? ComponentName = null,
    IReadOnlyList<string>? Fields = null)
{
    public IReadOnlyList<string> Fields { get; init; } = Fields ?? [];
}

/// <summary>
/// Rules of BSI TR-03183-2 v2.1.0 on a CycloneDX 1.6 document (docs/regeln-tr03183.md). Works on the
/// JSON, not on the internal model, so it checks own and foreign SBOMs the same way.
/// Gaps are read from properties named "&lt;namespace&gt;:gap", a logical component (TR 3.2.2) from
/// "&lt;namespace&gt;:logicalComponent" = "true"; any namespace except "cdx" and "bsi" counts.
/// </summary>
public static class TrValidator
{
    private static readonly string[] FileTraitNames =
        ["bsi:component:executable", "bsi:component:archive", "bsi:component:structured"];

    private static readonly Dictionary<string, (string Field, string[] Values)> FileTraits = new(StringComparer.Ordinal)
    {
        ["bsi:component:executable"] = (TrField.Executable, ["executable", "non-executable"]),
        ["bsi:component:archive"] = (TrField.Archive, ["archive", "no archive"]),
        ["bsi:component:structured"] = (TrField.Structured, ["structured", "unstructured"]),
    };

    /// <summary>Fields a logical component does not need to address (TR 3.2.2).</summary>
    private static readonly string[] NotForLogicalComponents =
        [TrField.Filename, TrField.Hash, TrField.Executable, TrField.Archive, TrField.Structured];

    public static IReadOnlyList<Finding> Validate(JsonObject bom)
    {
        var findings = new List<Finding>();
        CheckDocument(bom, findings);

        var components = new List<JsonObject>();
        if (bom["metadata"]?["component"] is JsonObject primary)
        {
            components.Add(primary);
        }
        Collect(bom["components"] as JsonArray, components);

        var refs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in components)
        {
            if (Text(component["bom-ref"]) is { } bomRef && !refs.Add(bomRef))
            {
                findings.Add(new(Severity.Error, "BOM_REF_DUPLICATE", $"Die bom-ref „{bomRef}“ kommt mehrfach vor.", bomRef, Text(component["name"]), [TrField.BomRef]));
            }
        }

        var dependencies = DependencyEntries(bom, refs, findings);
        var completeness = CompositionMembers(bom);
        foreach (var component in components)
        {
            CheckComponent(component, dependencies, completeness, findings);
        }
        CheckReachability(bom, components, findings);
        return findings;
    }

    /// <summary>
    /// TR 5.1: dependencies are resolved recursively from the primary component, so every component lies
    /// on a path from it. Edges are "dependsOn" and nesting (a contained component is a dependency, TR 3.2.7).
    /// </summary>
    private static void CheckReachability(JsonObject bom, List<JsonObject> components, List<Finding> findings)
    {
        if (bom["metadata"]?["component"] is not JsonObject primary || Text(primary["bom-ref"]) is not { } root)
        {
            return;
        }
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Edge(string from, string to)
        {
            if (!edges.TryGetValue(from, out var targets))
            {
                edges[from] = targets = [];
            }
            targets.Add(to);
        }
        foreach (var entry in (bom["dependencies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(entry["ref"]) is { } source)
            {
                foreach (var target in (entry["dependsOn"] as JsonArray ?? []).Select(Text).OfType<string>())
                {
                    Edge(source, target);
                }
            }
        }
        void Nest(JsonObject parent)
        {
            foreach (var child in (parent["components"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (Text(parent["bom-ref"]) is { } from && Text(child["bom-ref"]) is { } to)
                {
                    Edge(from, to);
                }
                Nest(child);
            }
        }
        Nest(primary);
        foreach (var top in (bom["components"] as JsonArray ?? []).OfType<JsonObject>())
        {
            Nest(top);
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal) { root };
        var queue = new Queue<string>([root]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var next in edges.GetValueOrDefault(current) ?? [])
            {
                if (reachable.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        foreach (var component in components)
        {
            if (Text(component["bom-ref"]) is { } bomRef && !reachable.Contains(bomRef))
            {
                findings.Add(new(Severity.Error, "COMPONENT_UNREACHABLE",
                    $"{bomRef}: Die Komponente ist von der Hauptkomponente aus über „dependencies“ nicht erreichbar. TR-03183-2 Abschnitt 5.1 verlangt die Auflösung der Abhängigkeiten von der Hauptkomponente aus; eine Abhängigkeitsliste auf dem Weg dorthin fehlt oder ist unvollständig.",
                    bomRef, Text(component["name"]), [TrField.DependencyGraph]));
            }
        }
    }

    private static void CheckDocument(JsonObject bom, List<Finding> findings)
    {
        if (bom["vulnerabilities"] is JsonArray { Count: > 0 })
        {
            findings.Add(new(Severity.Error, "VULNERABILITIES_PRESENT",
                "Die SBOM enthält Schwachstellen. TR-03183-2 Abschnitt 3.1 verbietet Schwachstelleninformationen in einer SBOM.",
                Fields: [TrField.SbomVulnerabilities]));
        }
        var metadata = bom["metadata"] as JsonObject;
        if (string.IsNullOrWhiteSpace(Text(metadata?["timestamp"])))
        {
            findings.Add(new(Severity.Error, "TIMESTAMP_MISSING", "metadata.timestamp fehlt (TR-03183-2 Abschnitt 5.2.1).", Fields: [TrField.SbomTimestamp]));
        }
        if (!HasContact(metadata?["manufacturer"] as JsonObject))
        {
            findings.Add(new(Severity.Error, "SBOM_CREATOR_MISSING",
                "Der Ersteller der SBOM fehlt: metadata.manufacturer braucht eine E-Mail-Adresse (contact) oder eine URL (TR-03183-2 Abschnitt 5.2.1).",
                Fields: [TrField.SbomCreator]));
        }
        if (metadata?["component"] is not JsonObject)
        {
            findings.Add(new(Severity.Error, "PRIMARY_COMPONENT_MISSING",
                "metadata.component fehlt. Ohne Hauptkomponente hat der Abhängigkeitsbaum keine Wurzel (TR-03183-2 Abschnitt 3.2.1).",
                Fields: [TrField.SbomPrimaryComponent]));
        }
    }

    private static void CheckComponent(
        JsonObject c,
        IReadOnlySet<string> dependencies,
        IReadOnlySet<string> completeness,
        List<Finding> findings)
    {
        var bomRef = Text(c["bom-ref"]);
        var name = Text(c["name"]) ?? "(ohne Namen)";
        var label = bomRef ?? name;
        var properties = Properties(c);

        var gaps = properties
            .Where(p => IsOwnNamespace(p.Name) && p.Name.EndsWith(":gap", StringComparison.Ordinal))
            .Select(p => p.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var logical = properties.Any(p => IsOwnNamespace(p.Name) && p.Name.EndsWith(":logicalComponent", StringComparison.Ordinal) && p.Value == "true");

        var missing = new List<string>();
        if (bomRef is null)
        {
            findings.Add(new(Severity.Error, "BOM_REF_MISSING",
                $"Komponente „{name}“ hat keine bom-ref; ohne sie lassen sich keine Abhängigkeiten angeben.", null, name, [TrField.BomRef]));
        }
        if (!HasContact(c["manufacturer"] as JsonObject))
        {
            missing.Add(TrField.Creator);
        }
        if (string.IsNullOrWhiteSpace(Text(c["version"])))
        {
            missing.Add(TrField.Version);
        }
        var filenames = properties.Count(p => p.Name == "bsi:component:filename");
        if (filenames == 0)
        {
            missing.Add(TrField.Filename);
        }
        else if (filenames > 1)
        {
            findings.Add(new(Severity.Error, "BSI_PROPERTY_DUPLICATE", $"{label}: bsi:component:filename kommt mehrfach vor.", bomRef, name, [TrField.Filename]));
        }
        var concluded = ConcludedLicences(c);
        if (concluded.Count == 0)
        {
            missing.Add(TrField.DistributionLicence);
        }
        foreach (var licence in concluded.Where(l => !l.Valid))
        {
            findings.Add(new(Severity.Error, "DISTRIBUTION_LICENCE_INVALID",
                $"{label}: Die Distributionslizenz „{licence.Text}“ ist weder eine SPDX-Kennung noch ein SPDX-Ausdruck noch eine Kennung LicenseRef-… (TR-03183-2 Abschnitt 6.1).",
                bomRef, name, [TrField.DistributionLicence]));
        }
        if (!HasDistributionSha512(c))
        {
            missing.Add(TrField.Hash);
        }
        foreach (var traitName in FileTraitNames)
        {
            var (field, allowed) = FileTraits[traitName];
            var values = properties.Where(p => p.Name == traitName).Select(p => p.Value).ToList();
            if (values.Count == 0)
            {
                missing.Add(field);
            }
            else if (values.Count > 1)
            {
                findings.Add(new(Severity.Error, "BSI_PROPERTY_DUPLICATE", $"{label}: {traitName} kommt mehrfach vor.", bomRef, name, [field]));
            }
            else if (!allowed.Contains(values[0], StringComparer.Ordinal))
            {
                findings.Add(new(Severity.Error, "BSI_PROPERTY_INVALID",
                    $"{label}: {traitName} hat den Wert „{values[0]}“, erlaubt sind „{allowed[0]}“ und „{allowed[1]}“.", bomRef, name, [field]));
            }
        }
        CheckLicenceProperties(properties, label, bomRef, name, findings);

        if (bomRef is not null && !dependencies.Contains(bomRef))
        {
            findings.Add(new(Severity.Error, "DEPENDENCIES_MISSING",
                $"{label}: Kein Eintrag in „dependencies“. Auch Komponenten ohne Abhängigkeiten brauchen einen leeren Eintrag (CycloneDX 1.6, TR-03183-2 Abschnitt 5.2.2).",
                bomRef, name, [TrField.Dependencies]));
        }
        if (bomRef is not null && !completeness.Contains(bomRef))
        {
            findings.Add(new(Severity.Error, "COMPLETENESS_MISSING",
                $"{label}: Die Vollständigkeit der Abhängigkeiten ist nicht angegeben; die Komponente fehlt in „compositions“ (TR-03183-2 Abschnitt 5.2.2).",
                bomRef, name, [TrField.DependencyCompleteness]));
        }

        if (logical)
        {
            var notApplicable = missing.Where(NotForLogicalComponents.Contains).ToList();
            missing.RemoveAll(notApplicable.Contains);
            findings.Add(new(Severity.Note, "LOGICAL_COMPONENT",
                $"{label} ist eine logische Komponente. Dateiname, Hash und die Eigenschaften ausführbar, Archiv und strukturiert entfallen (TR-03183-2 Abschnitt 3.2.2).",
                bomRef, name, notApplicable));
        }

        foreach (var code in gaps)
        {
            var definition = GapCatalog.Find(code);
            if (definition is null)
            {
                findings.Add(new(Severity.Warning, "GAP_UNKNOWN", $"{label}: Unbekannter Lückencode „{code}“.", bomRef, name));
                continue;
            }
            var excused = missing.Where(definition.Excuses.Contains).ToList();
            missing.RemoveAll(excused.Contains);
            findings.Add(new(definition.Severity, ToCode(code), $"{label}: {definition.Description}", bomRef, name, excused));
        }

        foreach (var field in missing)
        {
            findings.Add(new(Severity.Error, MissingCode(field), $"{label}: {MissingText(field)} Kein Lückenvermerk erklärt das Fehlen.", bomRef, name, [field]));
        }
    }

    private static void CheckLicenceProperties(List<(string Name, string Value)> properties, string label, string? bomRef, string name, List<Finding> findings)
    {
        var effective = properties.Count(p => p.Name == "bsi:component:effectiveLicence");
        if (effective > 1)
        {
            findings.Add(new(Severity.Error, "BSI_PROPERTY_DUPLICATE", $"{label}: bsi:component:effectiveLicence kommt mehrfach vor.", bomRef, name, [TrField.EffectiveLicence]));
        }
        if (properties.Any(p => p.Name == "bsi:component:effectiveLicense"))
        {
            findings.Add(new(Severity.Warning, "EFFECTIVE_LICENCE_SPELLING",
                $"{label}: bsi:component:effectiveLicense folgt der Schreibweise des TR-PDF. Die BSI-Taxonomie v0.1.2 schreibt bsi:component:effectiveLicence.",
                bomRef, name, [TrField.EffectiveLicence]));
        }
        if (properties.Any(p => p.Name == "bsi:component:associatedLicences"))
        {
            findings.Add(new(Severity.Warning, "ASSOCIATED_LICENCES_DEPRECATED",
                $"{label}: bsi:component:associatedLicences ist seit TR-03183-2 v2.1.0 veraltet.", bomRef, name));
        }
    }

    private static HashSet<string> DependencyEntries(JsonObject bom, HashSet<string> refs, List<Finding> findings)
    {
        var entries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in (bom["dependencies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(entry["ref"]) is not { } source)
            {
                continue;
            }
            entries.Add(source);
            foreach (var target in (entry["dependsOn"] as JsonArray ?? []).Select(Text).OfType<string>())
            {
                if (!refs.Contains(target))
                {
                    findings.Add(new(Severity.Error, "DEPENDENCY_REF_UNKNOWN",
                        $"{source} hängt von „{target}“ ab, diese bom-ref gibt es in der SBOM nicht.", source, null, [TrField.Dependencies]));
                }
            }
        }
        return entries;
    }

    private static HashSet<string> CompositionMembers(JsonObject bom)
    {
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var composition in (bom["compositions"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(composition["aggregate"]) is null)
            {
                continue;
            }
            foreach (var member in (composition["dependencies"] as JsonArray ?? []).Select(Text).OfType<string>())
            {
                members.Add(member);
            }
        }
        return members;
    }

    private static void Collect(JsonArray? array, List<JsonObject> target)
    {
        foreach (var component in (array ?? []).OfType<JsonObject>())
        {
            target.Add(component);
            Collect(component["components"] as JsonArray, target);
        }
    }

    private static List<(string Name, string Value)> Properties(JsonObject c) =>
        (c["properties"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(p => (Name: Text(p["name"]) ?? "", Value: Text(p["value"]) ?? ""))
            .ToList();

    private static bool IsOwnNamespace(string name) =>
        !name.StartsWith("cdx:", StringComparison.Ordinal) && !name.StartsWith("bsi:", StringComparison.Ordinal);

    /// <summary>TR mapping tables 8/9: manufacturer.url XOR manufacturer.contact[].email.</summary>
    private static bool HasContact(JsonObject? entity) =>
        entity is not null
        && ((entity["url"] as JsonArray ?? []).Select(Text).Any(u => !string.IsNullOrWhiteSpace(u))
            || (entity["contact"] as JsonArray ?? []).OfType<JsonObject>().Any(c => !string.IsNullOrWhiteSpace(Text(c["email"]))));

    /// <summary>
    /// Distribution licences: licenses[] with acknowledgement "concluded" (TR mapping table 9). Valid per
    /// TR 6.1: license.id (SPDX list, enforced by the schema), license.name only as LicenseRef-…, or an
    /// SPDX expression of such identifiers.
    /// </summary>
    private static List<(string Text, bool Valid)> ConcludedLicences(JsonObject c)
    {
        var result = new List<(string, bool)>();
        foreach (var entry in (c["licenses"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(entry["expression"]) is { } expression && Text(entry["acknowledgement"]) == "concluded")
            {
                result.Add((expression, SpdxLicense.IsValidExpression(expression)));
            }
            else if (entry["license"] is JsonObject license && Text(license["acknowledgement"]) == "concluded")
            {
                if (Text(license["id"]) is { } id)
                {
                    result.Add((id, true));
                }
                else if (Text(license["name"]) is { } licenceName)
                {
                    result.Add((licenceName, SpdxLicense.IsValidIdentifier(licenceName)));
                }
            }
        }
        return result;
    }

    /// <summary>Hash of the deployable form: externalReferences[type=distribution].hashes[alg=SHA-512].</summary>
    private static bool HasDistributionSha512(JsonObject c) =>
        (c["externalReferences"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(r => Text(r["type"]) == "distribution")
            .SelectMany(r => (r["hashes"] as JsonArray ?? []).OfType<JsonObject>())
            .Any(h => Text(h["alg"]) == "SHA-512" && !string.IsNullOrWhiteSpace(Text(h["content"])));

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    internal static string ToCode(string gapCode) => gapCode.ToUpperInvariant().Replace('-', '_');

    private static string MissingCode(string field) => field switch
    {
        TrField.Creator => "CREATOR_MISSING",
        TrField.Version => "VERSION_MISSING",
        TrField.Filename => "FILENAME_MISSING",
        TrField.DistributionLicence => "DISTRIBUTION_LICENCE_MISSING",
        TrField.Hash => "HASH_MISSING",
        TrField.Executable => "EXECUTABLE_PROPERTY_MISSING",
        TrField.Archive => "ARCHIVE_PROPERTY_MISSING",
        TrField.Structured => "STRUCTURED_PROPERTY_MISSING",
        _ => "FIELD_MISSING",
    };

    private static string MissingText(string field) => field switch
    {
        TrField.Creator => "Der Ersteller der Komponente fehlt; manufacturer braucht eine E-Mail-Adresse oder URL (TR-03183-2 Abschnitt 5.2.2).",
        TrField.Version => "Die Version fehlt (TR-03183-2 Abschnitt 5.2.2).",
        TrField.Filename => "bsi:component:filename fehlt (TR-03183-2 Abschnitt 5.2.2).",
        TrField.DistributionLicence => "Die Distributionslizenz fehlt; erwartet wird licenses mit acknowledgement „concluded“ (TR-03183-2 Abschnitt 5.2.2).",
        TrField.Hash => "Der SHA-512 der ausgelieferten Form fehlt (externalReferences vom Typ distribution, TR-03183-2 Abschnitt 5.2.2).",
        TrField.Executable => "bsi:component:executable fehlt (TR-03183-2 Abschnitt 5.2.2).",
        TrField.Archive => "bsi:component:archive fehlt (TR-03183-2 Abschnitt 5.2.2).",
        TrField.Structured => "bsi:component:structured fehlt (TR-03183-2 Abschnitt 5.2.2).",
        _ => $"{field} fehlt.",
    };
}
