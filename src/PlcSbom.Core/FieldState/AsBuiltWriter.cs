using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Identifiers;
using PlcSbom.Core.Output;
using PlcSbom.Core.Sbom;

namespace PlcSbom.Core.FieldState;

/// <summary>As-built SBOM and what changed against the projected one.</summary>
public sealed record AsBuiltResult(byte[] Sbom, IReadOnlyList<FieldWarning> Warnings, int FirmwareChanged);

/// <summary>
/// compare --as-built (CLAUDE.md sections 8.1, 8.3): the SBOM of one machine with the firmware found in the
/// field. Works on the JSON of the projected SBOM and changes only what the field file states: version and
/// firmware:origin = as-found of every matched firmware with a field version, serial number and hardware
/// revision of the device, the serial number of the machine. Gaps the field state closes are removed. If
/// the version changes, a CPE whose version field carries the old version gets the new one, otherwise it
/// is removed; a vendor SBOM that describes the old version is detached (E-023).
/// </summary>
public static class AsBuiltWriter
{
    public static AsBuiltResult Write(SbomView projected, Comparison comparison, DateTimeOffset timestamp)
    {
        var ns = projected.Namespace ?? throw new InvalidOperationException("Ohne Namespace kein Abgleich.");
        var root = (JsonObject)projected.Root.DeepClone();
        var view = SbomView.From(root);
        var warnings = new List<FieldWarning>();
        var field = comparison.Field;
        var changed = 0;
        var detached = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in comparison.Entries.Where(e => e.Sbom is not null && e.Field is not null))
        {
            var component = view.Find(entry.Sbom!.BomRef!)!;
            var json = component.Json;
            var f = entry.Field!;
            var properties = Properties(json);

            Set(properties, $"{ns}:device:serialNumber", f.Serial);
            Set(properties, $"{ns}:device:hardwareRevision", f.HardwareRevision);
            Set(properties, $"{ns}:field:via", f.Via);
            if (f.Firmware is null)
            {
                Write(json, properties);
                continue;
            }

            var old = component.Version ?? component.Own("firmware:version");
            var same = old is not null && FirmwareVersions.Compare(old, f.Firmware) == VersionMatch.Equal;
            if (!same)
            {
                changed++;
            }
            SetVersion(json, f.Firmware);
            Set(properties, $"{ns}:firmware:version", f.Firmware);
            Set(properties, $"{ns}:firmware:origin", "as-found");
            Set(properties, $"{ns}:firmware:projected", old);
            properties.RemoveAll(p => p.Key == $"{ns}:gap" && p.Value is GapCatalog.FirmwareProjectedOnly or GapCatalog.FirmwareNotInProject);

            if (!same && component.Cpe is { } cpe)
            {
                var replaced = old is null ? null : ReplaceCpeVersion(cpe, old, f.Firmware);
                if (replaced is not null)
                {
                    json["cpe"] = replaced;
                }
                else
                {
                    json.Remove("cpe");
                    warnings.Add(new FieldWarning("AS_BUILT_CPE_REMOVED",
                        $"{component.BomRef}: Die CPE {cpe} enthält die projektierte Version nicht im Versionsfeld. Sie ist in der as-built-SBOM entfernt, statt eine CPE für {f.Firmware} zu raten."));
                }
            }

            if (!same && component.InputSbom is { } input)
            {
                Detach(root, json, properties, component.BomRef!, ns);
                detached.Add(input);
                warnings.Add(new FieldWarning("AS_BUILT_VENDOR_SBOM_DETACHED",
                    $"{component.BomRef}: Die Hersteller-SBOM {input} beschreibt {old}, im Feld läuft {f.Firmware}. Sie ist in der as-built-SBOM nicht mehr zugeordnet."));
            }
            Write(json, properties);
        }

        RemoveUnusedVendorComponents(root, detached);

        var primary = root["metadata"]!["component"] as JsonObject;
        if (primary is not null)
        {
            var properties = Properties(primary);
            Set(properties, $"{ns}:machine:serialNumber", field.SerialNumber);
            Set(properties, $"{ns}:field:source", field.SourcePath);
            Set(properties, $"{ns}:field:sha256", field.Sha256);
            Set(properties, $"{ns}:field:date", field.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Write(primary, properties);

            // Same serial number as build would write for this machine (CLAUDE.md section 8.4).
            var name = SbomView.Text(primary["name"]) ?? "";
            var release = SbomView.Text(primary["version"]) ?? "";
            var commit = SbomView.Properties(primary).FirstOrDefault(p => p.Key == $"{ns}:git:commit").Value ?? "";
            root["serialNumber"] = $"urn:uuid:{Uuid5.Create(Uuid5.Namespace, string.Join('\n', name, release, commit, field.SerialNumber)):D}";
        }
        if (root["metadata"] is JsonObject metadata)
        {
            metadata["timestamp"] = timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        return new AsBuiltResult(Serialize(root), warnings, changed);
    }

    /// <summary>"cpe:2.3:o:siemens:x_firmware:4.5:…" with old version 4.5 (without "V") in the version field.</summary>
    internal static string? ReplaceCpeVersion(string cpe, string oldVersion, string newVersion)
    {
        const string prefix = "cpe:2.3:";
        if (!cpe.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }
        var parts = CpeText.Split(cpe[prefix.Length..]);
        if (parts.Count != 11 || parts[3] != CpeMap.Escape(CpeMap.VersionWithoutPrefix(oldVersion)))
        {
            return null;
        }
        parts[3] = CpeMap.Escape(CpeMap.VersionWithoutPrefix(newVersion));
        return prefix + string.Join(':', parts);
    }

    /// <summary>The firmware no longer has the vendor SBOM: no parts, file name, hash; the gaps come back.</summary>
    private static void Detach(JsonObject root, JsonObject firmware, List<KeyValuePair<string, string>> properties, string bomRef, string ns)
    {
        properties.RemoveAll(p => p.Key is "bsi:component:filename" || p.Key == $"{ns}:inputSbom" || p.Key == $"{ns}:inputSbom:sha256");
        foreach (var gap in new[] { GapCatalog.FilenameUnavailable, GapCatalog.HashUnavailable, GapCatalog.VendorSbomMissing })
        {
            Set(properties, $"{ns}:gap", gap, add: true);
        }
        if (firmware["externalReferences"] is JsonArray references)
        {
            foreach (var reference in references.OfType<JsonObject>().Where(r => SbomView.Text(r["type"]) == "distribution").ToList())
            {
                references.Remove(reference);
            }
            if (references.Count == 0)
            {
                firmware.Remove("externalReferences");
            }
        }
        foreach (var dependency in (root["dependencies"] as JsonArray ?? []).OfType<JsonObject>().Where(d => SbomView.Text(d["ref"]) == bomRef))
        {
            dependency["dependsOn"] = new JsonArray();
        }
        SetCompleteness(root, bomRef, "unknown");
    }

    /// <summary>Components of a detached vendor SBOM that no firmware of the machine uses any more.</summary>
    private static void RemoveUnusedVendorComponents(JsonObject root, HashSet<string> detached)
    {
        var view = SbomView.From(root);
        var stillUsed = view.Components.Where(c => c.Type == "firmware" && c.InputSbom is not null).Select(c => c.InputSbom!).ToHashSet(StringComparer.Ordinal);
        var remove = view.Components
            .Where(c => c.InputSbom is { } input && detached.Contains(input) && !stillUsed.Contains(input) && c.Own("inputSbom:kind") is null)
            .Select(c => c.BomRef)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (remove.Count == 0)
        {
            return;
        }
        void Prune(JsonArray? components)
        {
            foreach (var component in (components ?? []).OfType<JsonObject>().ToList())
            {
                if (SbomView.Text(component["bom-ref"]) is { } r && remove.Contains(r))
                {
                    components!.Remove(component);
                }
                else
                {
                    Prune(component["components"] as JsonArray);
                }
            }
        }
        Prune(root["components"] as JsonArray);
        if (root["dependencies"] is JsonArray dependencies)
        {
            foreach (var dependency in dependencies.OfType<JsonObject>().ToList())
            {
                if (SbomView.Text(dependency["ref"]) is { } r && remove.Contains(r))
                {
                    dependencies.Remove(dependency);
                    continue;
                }
                if (dependency["dependsOn"] is JsonArray targets)
                {
                    foreach (var target in targets.Where(t => SbomView.Text(t) is { } v && remove.Contains(v)).ToList())
                    {
                        targets.Remove(target);
                    }
                }
            }
        }
        foreach (var composition in (root["compositions"] as JsonArray ?? []).OfType<JsonObject>().ToList())
        {
            if (composition["dependencies"] is JsonArray members)
            {
                foreach (var member in members.Where(m => SbomView.Text(m) is { } v && remove.Contains(v)).ToList())
                {
                    members.Remove(member);
                }
                if (members.Count == 0)
                {
                    ((JsonArray)root["compositions"]!).Remove(composition);
                }
            }
        }
    }

    private static void SetCompleteness(JsonObject root, string bomRef, string aggregate)
    {
        if (root["compositions"] is not JsonArray compositions)
        {
            return;
        }
        foreach (var composition in compositions.OfType<JsonObject>())
        {
            if (composition["dependencies"] is JsonArray members)
            {
                foreach (var member in members.Where(m => SbomView.Text(m) == bomRef).ToList())
                {
                    members.Remove(member);
                }
            }
        }
        var target = compositions.OfType<JsonObject>().FirstOrDefault(c => SbomView.Text(c["aggregate"]) == aggregate);
        if (target is null)
        {
            target = new JsonObject { ["aggregate"] = aggregate, ["dependencies"] = new JsonArray() };
            compositions.Add(target);
        }
        var list = (JsonArray)target["dependencies"]!;
        var sorted = list.Select(SbomView.Text).OfType<string>().Append(bomRef).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        list.Clear();
        foreach (var member in sorted)
        {
            list.Add(member);
        }
        foreach (var empty in compositions.OfType<JsonObject>().Where(c => c["dependencies"] is JsonArray { Count: 0 }).ToList())
        {
            compositions.Remove(empty);
        }
    }

    /// <summary>"version" directly after "name", where the writer puts it.</summary>
    private static void SetVersion(JsonObject component, string version)
    {
        if (component.ContainsKey("version"))
        {
            component["version"] = version;
            return;
        }
        var index = component.IndexOf("name");
        component.Insert(index < 0 ? component.Count : index + 1, "version", version);
    }

    private static List<KeyValuePair<string, string>> Properties(JsonObject component) => SbomView.Properties(component).ToList();

    private static void Set(List<KeyValuePair<string, string>> properties, string name, string? value, bool add = false)
    {
        if (value is null)
        {
            return;
        }
        if (!add)
        {
            properties.RemoveAll(p => p.Key == name);
        }
        if (!properties.Contains(new(name, value)))
        {
            properties.Add(new(name, value));
        }
    }

    /// <summary>Properties sorted by name and value, like the writer.</summary>
    private static void Write(JsonObject component, List<KeyValuePair<string, string>> properties)
    {
        var array = new JsonArray();
        foreach (var (name, value) in properties.Distinct().OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal))
        {
            array.Add(new JsonObject { ["name"] = name, ["value"] = value });
        }
        if (component.ContainsKey("properties"))
        {
            component["properties"] = array;
        }
        else
        {
            component.Add("properties", array);
        }
    }

    /// <summary>Same layout as the CycloneDX writer: two spaces, LF, no BOM, final newline.</summary>
    internal static byte[] Serialize(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            node.WriteTo(writer);
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }
}
