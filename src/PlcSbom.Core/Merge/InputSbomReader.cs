using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Core.Validation;

namespace PlcSbom.Core.Merge;

/// <summary>What identifies the firmware a vendor SBOM describes: vendor, order number, version, CPE of its metadata.component.</summary>
public sealed record VendorSbomKey(string? Vendor, string? OrderNumber, string? Version, string? Cpe);

/// <summary>The deployable file a vendor SBOM states for its firmware: bsi:component:filename and the SHA-512 under distribution.</summary>
public sealed record DeployableFile(string? FileName, string? Uri, string? Sha512);

/// <summary>Result of reading one input SBOM: the prepared SBOM (null if unusable) and the findings about it.</summary>
public sealed record InputSbomRead(InputSbom? Sbom, VendorSbomKey? Key, DeployableFile? File, string? ToolName, IReadOnlyList<Finding> Findings);

/// <summary>
/// Reads a vendor or partial SBOM (CLAUDE.md section 5.7) and prepares it for the merge:
/// <list type="bullet">
/// <item>bom-refs get the prefix of the input, so they are unique in the machine SBOM;</item>
/// <item>the effective licence is removed: the creator of the current SBOM sets it anew (TR 3.2.8);</item>
/// <item>components lacking required fields are marked input-field-missing (warning), the input is checked against the TR on its own;</item>
/// <item>the root (metadata.component) depends on every component of the input no other one reaches, because the components of a BOM make up its primary component.</item>
/// </list>
/// Everything else stays as read.
/// </summary>
public static class InputSbomReader
{
    private static readonly string[] EffectiveLicenceNames = ["bsi:component:effectiveLicence", "bsi:component:effectiveLicense"];

    public static async Task<InputSbomRead> ReadAsync(string fullPath, string displayPath, InputSbomKind kind, string id, string? station)
    {
        var what = kind == InputSbomKind.Vendor ? "Hersteller-SBOM" : "Teil-SBOM";
        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(fullPath).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Invalid($"Die {what} {displayPath} ist nicht lesbar: {ex.Message}");
        }
        var text = Encoding.UTF8.GetString(content);
        JsonObject bom;
        try
        {
            bom = JsonNode.Parse(text) as JsonObject ?? throw new JsonException("Kein JSON-Objekt.");
        }
        catch (JsonException ex)
        {
            return Invalid($"Die {what} {displayPath} ist kein gültiges JSON: {ex.Message}");
        }
        if (Text(bom["bomFormat"]) != "CycloneDX")
        {
            return Invalid($"Die {what} {displayPath} ist keine CycloneDX-SBOM (bomFormat fehlt oder ist nicht „CycloneDX“).");
        }
        if (bom["metadata"]?["component"] is not JsonObject primary)
        {
            return Invalid($"Die {what} {displayPath} hat keine metadata.component. Ohne sie lässt sie sich keiner Firmware, Station oder Maschine zuordnen.");
        }

        var findings = new List<Finding>();
        var report = await SbomValidator.ValidateAsync(text).ConfigureAwait(false);
        if (report.Errors > 0 || report.Warnings > 0)
        {
            findings.Add(new Finding(Severity.Warning, kind == InputSbomKind.Vendor ? "VENDOR_SBOM_NOT_TR_CONFORM" : "PARTIAL_SBOM_NOT_TR_CONFORM",
                $"Die {what} {displayPath} erfüllt BSI TR-03183-2 nicht ({report.Errors} Fehler, {report.Warnings} Warnungen). Ihre Komponenten sind übernommen, fehlende Pflichtfelder mit input-field-missing vermerkt. Einzelheiten zeigt plc-sbom validate {displayPath}."));
        }

        var root = (JsonObject)primary.DeepClone();
        var components = (bom["components"] as JsonArray ?? []).OfType<JsonObject>().Select(c => (JsonObject)c.DeepClone()).ToList();
        // Components nested in the root become top-level components the root depends on, because the
        // root of a vendor SBOM is not written again (it is the firmware).
        var nestedInRoot = (root["components"] as JsonArray ?? []).OfType<JsonObject>().Select(c => (JsonObject)c.DeepClone()).ToList();
        root.Remove("components");
        components.InsertRange(0, nestedInRoot);

        // Prefix every bom-ref; components without one get a generated ref in document order.
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var generated = 0;
        var all = new List<JsonObject>();
        foreach (var component in new[] { root }.Concat(components))
        {
            Collect(component, all);
        }
        foreach (var component in all)
        {
            var original = Text(component["bom-ref"]);
            if (original is null)
            {
                component["bom-ref"] = $"{id}/component-{++generated}";
                continue;
            }
            if (map.ContainsKey(original))
            {
                return Invalid($"Die {what} {displayPath} enthält die bom-ref „{original}“ mehrfach. Abhängigkeiten lassen sich dann nicht eindeutig zuordnen.");
            }
            map[original] = $"{id}/{original}";
            component["bom-ref"] = map[original];
        }
        foreach (var component in all)
        {
            RemoveEffectiveLicence(component);
        }
        var rootRef = Text(root["bom-ref"])!;
        var nestedRefs = nestedInRoot.Select(c => Text(c["bom-ref"])!).ToList();

        var dependsOn = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var unknownRefs = 0;
        foreach (var entry in (bom["dependencies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(entry["ref"]) is not { } source || !map.TryGetValue(source, out var from))
            {
                unknownRefs++;
                continue;
            }
            var targets = new List<string>();
            foreach (var target in (entry["dependsOn"] as JsonArray ?? []).Select(Text).OfType<string>())
            {
                if (map.TryGetValue(target, out var to))
                {
                    targets.Add(to);
                }
                else
                {
                    unknownRefs++;
                }
            }
            dependsOn[from] = targets.Distinct(StringComparer.Ordinal).ToList();
        }
        if (nestedRefs.Count > 0)
        {
            dependsOn[rootRef] = [.. (dependsOn.GetValueOrDefault(rootRef) ?? []).Union(nestedRefs, StringComparer.Ordinal)];
        }
        if (unknownRefs > 0)
        {
            findings.Add(new Finding(Severity.Warning, "INPUT_SBOM_REF_UNKNOWN",
                $"Die {what} {displayPath} nennt in „dependencies“ {unknownRefs} bom-ref(s), die es in ihr nicht gibt. Diese Angaben sind nicht übernommen."));
        }

        // The components of a BOM make up its primary component: the root depends on every top-level
        // component that nothing else in the input reaches.
        // First those no other component depends on, then whatever is still unreachable (a cycle).
        var referenced = dependsOn.Values.SelectMany(t => t).ToHashSet(StringComparer.Ordinal);
        foreach (var onlyUnreferenced in new[] { true, false })
        {
            var reached = Reachable(rootRef, dependsOn, all);
            var orphans = components
                .Select(c => Text(c["bom-ref"])!)
                .Where(r => !reached.Contains(r) && (!onlyUnreferenced || !referenced.Contains(r)))
                .ToList();
            if (orphans.Count > 0)
            {
                dependsOn[rootRef] = [.. dependsOn.GetValueOrDefault(rootRef) ?? [], .. orphans];
            }
        }

        var completeness = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var composition in (bom["compositions"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(composition["aggregate"]) is not { } aggregate)
            {
                continue;
            }
            foreach (var member in (composition["dependencies"] as JsonArray ?? []).Select(Text).OfType<string>())
            {
                if (map.TryGetValue(member, out var prefixed))
                {
                    completeness.TryAdd(prefixed, aggregate);
                }
            }
        }

        var gaps = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var component in all)
        {
            if (TrValidator.UnexplainedMissingFields(component).Count > 0)
            {
                gaps[Text(component["bom-ref"])!] = [GapCatalog.InputFieldMissing];
            }
        }

        var sbom = new InputSbom
        {
            Id = id,
            Kind = kind,
            SourcePath = displayPath,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
            Station = station,
            Root = root,
            Components = components,
            DependsOn = dependsOn,
            Completeness = completeness,
            Gaps = gaps,
        };
        return new InputSbomRead(sbom, Key(primary), Deployable(primary), ToolName(bom), findings);
    }

    /// <summary>
    /// Vendor (supplier, else manufacturer), version and CPE of the primary component; the order number
    /// from a property "&lt;namespace&gt;:device:orderNumber" as plc-sbom writes it (any namespace except cdx and bsi).
    /// </summary>
    private static VendorSbomKey Key(JsonObject primary)
    {
        var orderNumber = (primary["properties"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(p => Text(p["name"]) is { } name
                && name.EndsWith(":device:orderNumber", StringComparison.Ordinal)
                && !name.StartsWith("cdx:", StringComparison.Ordinal)
                && !name.StartsWith("bsi:", StringComparison.Ordinal))
            .Select(p => Text(p["value"]))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return new VendorSbomKey(
            Text(primary["supplier"]?["name"]) ?? Text(primary["manufacturer"]?["name"]),
            orderNumber,
            Text(primary["version"]),
            Text(primary["cpe"]));
    }

    /// <summary>File name and SHA-512 of the deployable form as the primary component states them (TR mapping table 9).</summary>
    private static DeployableFile Deployable(JsonObject primary)
    {
        var fileName = (primary["properties"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(p => Text(p["name"]) == "bsi:component:filename")
            .Select(p => Text(p["value"]))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        var distribution = (primary["externalReferences"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(r => Text(r["type"]) == "distribution" && !string.IsNullOrWhiteSpace(Text(r["url"])))
            .Select(r => (Url: Text(r["url"])!, Sha512: (r["hashes"] as JsonArray ?? []).OfType<JsonObject>()
                .Where(h => Text(h["alg"]) == "SHA-512")
                .Select(h => Text(h["content"]))
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))))
            .FirstOrDefault(r => r.Sha512 is not null);
        return new DeployableFile(fileName, distribution.Url, distribution.Sha512);
    }

    /// <summary>Name of the first tool in metadata.tools (CycloneDX 1.5+ object or the older array).</summary>
    private static string? ToolName(JsonObject bom) => bom["metadata"]?["tools"] switch
    {
        JsonObject tools => (tools["components"] as JsonArray ?? []).OfType<JsonObject>().Select(t => Text(t["name"])).FirstOrDefault(n => n is not null),
        JsonArray tools => tools.OfType<JsonObject>().Select(t => Text(t["name"])).FirstOrDefault(n => n is not null),
        _ => null,
    };

    private static HashSet<string> Reachable(string root, IReadOnlyDictionary<string, IReadOnlyList<string>> dependsOn, List<JsonObject> all)
    {
        var edges = dependsOn.ToDictionary(d => d.Key, d => d.Value.ToList(), StringComparer.Ordinal);
        foreach (var parent in all)
        {
            foreach (var child in (parent["components"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var from = Text(parent["bom-ref"])!;
                if (!edges.TryGetValue(from, out var list))
                {
                    edges[from] = list = [];
                }
                list.Add(Text(child["bom-ref"])!);
            }
        }
        var reached = new HashSet<string>(StringComparer.Ordinal) { root };
        var queue = new Queue<string>([root]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var next in edges.GetValueOrDefault(current) ?? [])
            {
                if (reached.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }
        return reached;
    }

    private static void Collect(JsonObject component, List<JsonObject> target)
    {
        target.Add(component);
        foreach (var child in (component["components"] as JsonArray ?? []).OfType<JsonObject>())
        {
            Collect(child, target);
        }
    }

    private static void RemoveEffectiveLicence(JsonObject component)
    {
        if (component["properties"] is not JsonArray properties)
        {
            return;
        }
        foreach (var property in properties.OfType<JsonObject>().Where(p => EffectiveLicenceNames.Contains(Text(p["name"]))).ToList())
        {
            properties.Remove(property);
        }
    }

    private static InputSbomRead Invalid(string message) =>
        new(null, null, null, null, [new Finding(Severity.Error, "INPUT_SBOM_INVALID", message)]);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
