using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;

namespace PlcSbom.Core.Output;

/// <summary>Settings of one write. Clock and namespace come in from outside so the output is reproducible.</summary>
public sealed record WriterOptions
{
    /// <summary>Property namespace of the user (machine.yml "namespace"); "cdx" is reserved by the taxonomy.</summary>
    public string Namespace { get; init; } = "plcsbom";

    /// <summary>metadata.timestamp, written in UTC with seconds precision.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    public string ToolVersion { get; init; } = Core.ToolVersion.Short;
}

/// <summary>
/// Writes the model as CycloneDX 1.6 JSON, variant B (ADR E-007): only software components
/// (firmware, application, library); device and station are properties of the firmware, the structure
/// is in "dependencies". The machine is the primary component in metadata.component.
/// Output is deterministic: fixed field order, sorted components, properties and dependencies,
/// LF line endings, no BOM.
/// </summary>
public static class CycloneDxWriter
{
    public const string FileTraitExecutable = "bsi:component:executable";
    public const string FileTraitArchive = "bsi:component:archive";
    public const string FileTraitStructured = "bsi:component:structured";
    public const string BsiFilename = "bsi:component:filename";
    public const string BsiEffectiveLicence = "bsi:component:effectiveLicence";

    public static byte[] Write(Machine machine, WriterOptions options)
    {
        if (options.Namespace is "cdx" or "bsi" || string.IsNullOrWhiteSpace(options.Namespace))
        {
            throw new ArgumentException($"Der Property-Namespace „{options.Namespace}“ ist reserviert oder leer.", nameof(options));
        }
        var bom = Build(machine, options);
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            WriteBom(json, bom);
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    // ---- Model → draft ------------------------------------------------------------------------

    private sealed record Draft(
        string BomRef,
        string Type,
        string Name,
        string? Version,
        Party? Vendor,
        Licensing? Licensing,
        string? Cpe,
        string? Purl,
        IReadOnlyList<ExternalRef> ExternalReferences,
        IReadOnlyList<KeyValuePair<string, string>> Properties);

    private sealed record ExternalRef(string Type, string Url, string? Sha512);

    private enum Completeness { Complete, Incomplete, Unknown }

    private sealed record BomDraft(
        Machine Machine,
        WriterOptions Options,
        Draft Primary,
        IReadOnlyList<Draft> Components,
        IReadOnlyDictionary<string, SortedSet<string>> DependsOn,
        IReadOnlyDictionary<string, Completeness> Completeness,
        IReadOnlyList<ToolInfo> Tools);

    private static BomDraft Build(Machine machine, WriterOptions o)
    {
        var ns = o.Namespace;
        var refs = new BomRef.Registry();
        var components = new List<Draft>();
        var dependsOn = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var completeness = new Dictionary<string, Completeness>(StringComparer.Ordinal);

        var primaryRef = refs.Claim(BomRef.Slug(machine.Name));
        dependsOn[primaryRef] = new SortedSet<string>(StringComparer.Ordinal);
        completeness[primaryRef] = Completeness.Complete;

        foreach (var station in machine.Stations)
        {
            var stationSlug = BomRef.Slug(station.Name);
            var firmwareRefs = new List<string>();
            string? controllerRef = null;

            foreach (var device in station.Devices.Where(d => !d.IsPassive))
            {
                var isController = device.IsController && controllerRef is null;
                var candidate = isController
                    ? $"{stationSlug}-fw"
                    : $"{stationSlug}-fw-{BomRef.Slug(device.Position ?? device.DeviceName ?? device.TypeName)}";
                var bomRef = refs.Claim(candidate);
                components.Add(Firmware(device, bomRef, station, ns));
                dependsOn[bomRef] = new SortedSet<string>(StringComparer.Ordinal);
                completeness[bomRef] = Completeness.Unknown;
                firmwareRefs.Add(bomRef);
                if (isController)
                {
                    controllerRef = bomRef;
                }
            }

            var programRefs = new List<string>();
            var programDependencies = new List<string>();
            foreach (var item in station.Software)
            {
                var bomRef = refs.Claim(SoftwareRef(stationSlug, item));
                components.Add(Software(item, bomRef, station, ns));
                dependsOn[bomRef] = new SortedSet<string>(StringComparer.Ordinal);
                if (item.Kind == SoftwareKind.UserProgram)
                {
                    programRefs.Add(bomRef);
                    var incomplete = station.Gaps.Concat(item.Gaps).Any(g => GapCatalog.MakeDependenciesIncomplete.Contains(g.Code));
                    completeness[bomRef] = incomplete ? Completeness.Incomplete : Completeness.Complete;
                }
                else
                {
                    programDependencies.Add(bomRef);
                    completeness[bomRef] = Completeness.Unknown;
                }
            }

            // Machine → every firmware and the programs; program → controller firmware and software.
            // Without a program, the station's software hangs directly under the machine.
            dependsOn[primaryRef].UnionWith(firmwareRefs);
            if (programRefs.Count > 0)
            {
                dependsOn[primaryRef].UnionWith(programRefs);
                foreach (var program in programRefs)
                {
                    dependsOn[program].UnionWith(programDependencies);
                    if (controllerRef is not null)
                    {
                        dependsOn[program].Add(controllerRef);
                    }
                }
            }
            else
            {
                dependsOn[primaryRef].UnionWith(programDependencies);
            }

            // Station-level gaps belong to the program, else to the controller firmware, else to the machine.
            if (station.Gaps.Count > 0)
            {
                var target = programRefs.FirstOrDefault() ?? controllerRef ?? primaryRef;
                if (target != primaryRef)
                {
                    var index = components.FindIndex(c => c.BomRef == target);
                    components[index] = components[index] with
                    {
                        Properties = [.. components[index].Properties, .. GapProperties(station.Gaps, ns)],
                    };
                }
            }
        }

        foreach (var device in machine.UnassignedDevices.Where(d => !d.IsPassive))
        {
            var bomRef = refs.Claim($"{primaryRef}-fw-{BomRef.Slug(device.DeviceName ?? device.Position ?? device.TypeName)}");
            components.Add(Firmware(device, bomRef, station: null, ns));
            dependsOn[bomRef] = new SortedSet<string>(StringComparer.Ordinal);
            completeness[bomRef] = Completeness.Unknown;
            dependsOn[primaryRef].Add(bomRef);
        }

        var stationGapsOnMachine = machine.Stations
            .Where(s => s.Gaps.Count > 0 && s.Devices.All(d => d.IsPassive) && s.Software.Count == 0)
            .SelectMany(s => s.Gaps);
        var primary = Primary(machine, primaryRef, ns, [.. machine.Gaps, .. stationGapsOnMachine]);

        var tools = machine.Stations
            .Select(s => s.EngineeringTool)
            .OfType<ToolInfo>()
            .Distinct()
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ThenBy(t => t.Version, StringComparer.Ordinal)
            .ThenBy(t => t.Release, StringComparer.Ordinal)
            .ToList();

        return new BomDraft(
            machine,
            o,
            primary,
            components.OrderBy(c => c.BomRef, StringComparer.Ordinal).ToList(),
            dependsOn,
            completeness,
            tools);
    }

    private static string SoftwareRef(string stationSlug, SoftwareItem item) => item.Kind switch
    {
        SoftwareKind.UserProgram => $"{stationSlug}-app",
        SoftwareKind.Library => $"{stationSlug}-lib-{BomRef.Slug(item.Name)}",
        SoftwareKind.LibraryType => $"{stationSlug}-lib-{BomRef.Slug(item.Library ?? "projekt")}-{BomRef.Slug(item.Name)}",
        SoftwareKind.ProtectedBlock => $"{stationSlug}-block-{BomRef.Slug(item.Name)}",
        SoftwareKind.Runtime => $"{stationSlug}-rt",
        SoftwareKind.AddOnInstruction => $"{stationSlug}-aoi-{BomRef.Slug(item.Name)}",
        _ => throw new ArgumentOutOfRangeException(nameof(item), item.Kind, null),
    };

    private static Draft Primary(Machine machine, string bomRef, string ns, IEnumerable<Gap> gaps)
    {
        var p = new PropertyList(ns);
        // TR 3.2.2: the machine is a logical component; filename, hash and the file properties do not apply.
        p.Add($"{ns}:logicalComponent", "true");
        p.Add($"{ns}:git:commit", machine.Commit);
        p.Add($"{ns}:machine:serialNumber", machine.SerialNumber);
        p.Add(BsiEffectiveLicence, machine.Licensing?.Effective);
        p.AddRange(GapProperties(gaps, ns));
        return new Draft(bomRef, "device", machine.Name, machine.Release, machine.Supplier, machine.Licensing, null, null, [], p.Items);
    }

    private static Draft Firmware(Device device, string bomRef, Station? station, string ns)
    {
        var p = new PropertyList(ns);
        AddTraits(p, FileTraits.Firmware);
        p.Add(BsiEffectiveLicence, device.FirmwareLicensing?.Effective);
        AddStation(p, station, ns);
        p.Add($"{ns}:device:vendor", device.Vendor?.Name);
        if (device.Vendor is not null)
        {
            p.Add($"{ns}:device:vendor:origin", Origin(device.VendorOrigin));
        }
        p.Add($"{ns}:device:orderNumber", device.OrderNumber);
        if (device.OrderNumberIsPattern)
        {
            p.Add($"{ns}:device:orderNumber:pattern", "true");
        }
        p.Add($"{ns}:device:type", device.TypeName);
        p.Add($"{ns}:device:name", device.DeviceName);
        p.Add($"{ns}:device:position", device.Position);
        p.Add($"{ns}:device:classification", device.Classification);
        p.Add($"{ns}:firmware:version", device.Firmware);
        p.Add($"{ns}:firmware:origin", device.Firmware is null ? "not-in-project" : FirmwareOriginText(device.FirmwareOrigin));
        foreach (var (key, value) in device.VendorKeys)
        {
            p.Add($"{ns}:vendorKey:{key}", value);
        }
        p.AddRange(GapProperties(device.Gaps, ns));
        return new Draft(bomRef, "firmware", $"{device.TypeName}, Firmware", device.Firmware, device.Vendor,
            device.FirmwareLicensing, device.Cpe, null, [], p.Items);
    }

    private static Draft Software(SoftwareItem item, string bomRef, Station station, string ns)
    {
        var p = new PropertyList(ns);
        if (item.Traits is not null)
        {
            AddTraits(p, item.Traits);
        }
        p.Add(BsiFilename, item.FileName);
        p.Add(BsiEffectiveLicence, item.Licensing?.Effective);
        AddStation(p, station, ns);
        p.Add($"{ns}:software:kind", KindText(item.Kind));
        p.Add($"{ns}:library", item.Library);
        p.Add($"{ns}:origin", Origin(item.Origin));
        if (item.Vendor is not null)
        {
            p.Add($"{ns}:vendor:origin", Origin(item.VendorOrigin));
        }
        if (!item.VersionResolved)
        {
            p.Add($"{ns}:version:requested", item.Version);
        }
        p.AddRange(GapProperties(item.Gaps, ns));

        var externalReferences = new List<ExternalRef>();
        AddReference(externalReferences, "distribution", item.DeployableUri, item.DeployableSha512, item.Name);
        AddReference(externalReferences, "source-distribution", item.SourceUri, item.SourceSha512, item.Name);

        var type = item.Kind is SoftwareKind.UserProgram or SoftwareKind.Runtime ? "application" : "library";
        var version = item.VersionResolved ? item.Version : null;
        return new Draft(bomRef, type, item.Name, version, item.Vendor, item.Licensing, item.Cpe, item.Purl, externalReferences, p.Items);
    }

    private static void AddReference(List<ExternalRef> target, string type, string? uri, string? sha512, string name)
    {
        if (uri is null)
        {
            if (sha512 is not null)
            {
                // CycloneDX requires a URL on every external reference; a hash without its location would need an invented URL.
                throw new InvalidOperationException($"Modellfehler: {name} hat einen SHA-512 für „{type}“, aber keine URI.");
            }
            return;
        }
        target.Add(new ExternalRef(type, uri, sha512));
    }

    private static void AddStation(PropertyList p, Station? station, string ns)
    {
        if (station is null)
        {
            return;
        }
        p.Add($"{ns}:adapter", station.AdapterId);
        p.Add($"{ns}:station", station.Name);
        p.Add($"{ns}:source", station.SourcePath);
        p.Add($"{ns}:source:sha256", station.SourceSha256);
        p.Add($"{ns}:source:exportedAt", station.SourceExportedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
    }

    private static void AddTraits(PropertyList p, FileTraits traits)
    {
        p.Add(FileTraitExecutable, traits.Executable ? "executable" : "non-executable");
        p.Add(FileTraitArchive, traits.Archive ? "archive" : "no archive");
        p.Add(FileTraitStructured, traits.Structured ? "structured" : "unstructured");
    }

    private static IEnumerable<KeyValuePair<string, string>> GapProperties(IEnumerable<Gap> gaps, string ns) =>
        gaps.Select(g => g.Code).Distinct(StringComparer.Ordinal).Select(code => new KeyValuePair<string, string>($"{ns}:gap", code));

    private static string Origin(ValueOrigin origin) => origin switch
    {
        ValueOrigin.Source => "source",
        ValueOrigin.Gsdml => "gsdml",
        ValueOrigin.Declared => "declared",
        ValueOrigin.Assumed => "assumed",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };

    private static string FirmwareOriginText(FirmwareOrigin origin) => origin switch
    {
        FirmwareOrigin.Projected => "projected",
        FirmwareOrigin.AsFound => "as-found",
        FirmwareOrigin.NotInProject => "not-in-project",
        FirmwareOrigin.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };

    private static string KindText(SoftwareKind kind) => kind switch
    {
        SoftwareKind.UserProgram => "user-program",
        SoftwareKind.Library => "library",
        SoftwareKind.LibraryType => "library-type",
        SoftwareKind.ProtectedBlock => "protected-block",
        SoftwareKind.Runtime => "runtime",
        SoftwareKind.AddOnInstruction => "add-on-instruction",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private sealed class PropertyList(string ns)
    {
        private readonly List<KeyValuePair<string, string>> items = [];

        public IReadOnlyList<KeyValuePair<string, string>> Items => items;

        public void Add(string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                items.Add(new(name, value));
            }
        }

        public void AddRange(IEnumerable<KeyValuePair<string, string>> values) => items.AddRange(values);

        public string Namespace => ns;
    }

    // ---- Draft → JSON -------------------------------------------------------------------------

    private static void WriteBom(Utf8JsonWriter w, BomDraft bom)
    {
        var m = bom.Machine;
        w.WriteStartObject();
        w.WriteString("$schema", "http://cyclonedx.org/schema/bom-1.6.schema.json");
        w.WriteString("bomFormat", "CycloneDX");
        w.WriteString("specVersion", "1.6");
        // SBOM-URI (TR 5.2.3): stable per machine, release and commit (CLAUDE.md section 8.4).
        var serial = Uuid5.Create(Uuid5.Namespace, string.Join('\n', m.Name, m.Release, m.Commit ?? "", m.SerialNumber ?? ""));
        w.WriteString("serialNumber", $"urn:uuid:{serial:D}");
        w.WriteNumber("version", 1);

        w.WriteStartObject("metadata");
        w.WriteString("timestamp", bom.Options.Timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        w.WriteStartObject("tools");
        w.WriteStartArray("components");
        WriteTool(w, "PLC SBOM Builder", bom.Options.ToolVersion, "Comquent GmbH", null, bom.Options.Namespace);
        foreach (var tool in bom.Tools)
        {
            WriteTool(w, tool.Name, tool.Version, tool.Vendor, tool.Release, bom.Options.Namespace);
        }
        w.WriteEndArray();
        w.WriteEndObject();
        // Creator of the SBOM (TR 5.2.1, mapping table 8): metadata.manufacturer.
        w.WritePropertyName("manufacturer");
        WriteEntity(w, m.Supplier);
        w.WritePropertyName("component");
        WriteComponent(w, bom.Primary);
        w.WriteEndObject();

        w.WriteStartArray("components");
        foreach (var component in bom.Components)
        {
            WriteComponent(w, component);
        }
        w.WriteEndArray();

        // Every component has an entry, also without dependencies (CycloneDX 1.6, TR mapping table 9).
        w.WriteStartArray("dependencies");
        foreach (var (bomRef, targets) in bom.DependsOn.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            w.WriteStartObject();
            w.WriteString("ref", bomRef);
            w.WriteStartArray("dependsOn");
            foreach (var target in targets)
            {
                w.WriteStringValue(target);
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();

        // Completeness of each dependency list (TR 5.2.2, CLAUDE.md section 4.4).
        w.WriteStartArray("compositions");
        foreach (var (state, text) in new[] { (Completeness.Complete, "complete"), (Completeness.Incomplete, "incomplete"), (Completeness.Unknown, "unknown") })
        {
            var members = bom.Completeness.Where(c => c.Value == state).Select(c => c.Key).Order(StringComparer.Ordinal).ToList();
            if (members.Count == 0)
            {
                continue;
            }
            w.WriteStartObject();
            w.WriteString("aggregate", text);
            w.WriteStartArray("dependencies");
            foreach (var member in members)
            {
                w.WriteStringValue(member);
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();

        w.WriteEndObject();
    }

    private static void WriteTool(Utf8JsonWriter w, string name, string version, string? vendor, string? release, string ns)
    {
        w.WriteStartObject();
        w.WriteString("type", "application");
        if (vendor is not null)
        {
            w.WriteStartObject("manufacturer");
            w.WriteString("name", vendor);
            w.WriteEndObject();
        }
        w.WriteString("name", name);
        w.WriteString("version", version);
        if (release is not null)
        {
            WriteProperties(w, [new($"{ns}:tool:release", release)]);
        }
        w.WriteEndObject();
    }

    private static void WriteComponent(Utf8JsonWriter w, Draft c)
    {
        w.WriteStartObject();
        w.WriteString("bom-ref", c.BomRef);
        w.WriteString("type", c.Type);
        if (c.Vendor is not null)
        {
            w.WriteStartObject("supplier");
            w.WriteString("name", c.Vendor.Name);
            w.WriteEndObject();
            // Component creator (TR 5.2.2, mapping table 9): manufacturer with url or contact email.
            w.WritePropertyName("manufacturer");
            WriteEntity(w, c.Vendor);
        }
        w.WriteString("name", c.Name);
        if (c.Version is not null)
        {
            w.WriteString("version", c.Version);
        }
        WriteLicenses(w, c.Licensing);
        if (c.Cpe is not null)
        {
            w.WriteString("cpe", c.Cpe);
        }
        if (c.Purl is not null)
        {
            w.WriteString("purl", c.Purl);
        }
        if (c.ExternalReferences.Count > 0)
        {
            w.WriteStartArray("externalReferences");
            foreach (var reference in c.ExternalReferences.OrderBy(r => r.Type, StringComparer.Ordinal))
            {
                w.WriteStartObject();
                w.WriteString("url", reference.Url);
                w.WriteString("type", reference.Type);
                if (reference.Sha512 is not null)
                {
                    w.WriteStartArray("hashes");
                    w.WriteStartObject();
                    w.WriteString("alg", "SHA-512");
                    w.WriteString("content", reference.Sha512);
                    w.WriteEndObject();
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        WriteProperties(w, c.Properties);
        w.WriteEndObject();
    }

    private static void WriteEntity(Utf8JsonWriter w, Party party)
    {
        w.WriteStartObject();
        w.WriteString("name", party.Name);
        if (!string.IsNullOrWhiteSpace(party.Url))
        {
            w.WriteStartArray("url");
            w.WriteStringValue(party.Url);
            w.WriteEndArray();
        }
        if (!string.IsNullOrWhiteSpace(party.Email))
        {
            w.WriteStartArray("contact");
            w.WriteStartObject();
            w.WriteString("email", party.Email);
            w.WriteEndObject();
            w.WriteEndArray();
        }
        w.WriteEndObject();
    }

    /// <summary>
    /// Distribution licence → acknowledgement "concluded", original licence → "declared" (TR mapping
    /// tables 9 and 11). CycloneDX 1.6 allows either exactly one expression or a list of single licences,
    /// so both together are only possible if both are single identifiers.
    /// </summary>
    private static void WriteLicenses(Utf8JsonWriter w, Licensing? licensing)
    {
        if (licensing?.Distribution is null && licensing?.Original is null)
        {
            return;
        }
        w.WriteStartArray("licenses");
        if (licensing.Original is null || licensing.Distribution is null)
        {
            w.WriteStartObject();
            w.WriteString("expression", licensing.Distribution ?? licensing.Original);
            w.WriteString("acknowledgement", licensing.Distribution is not null ? "concluded" : "declared");
            w.WriteEndObject();
        }
        else
        {
            if (!SpdxLicense.IsSingleIdentifier(licensing.Distribution) || !SpdxLicense.IsSingleIdentifier(licensing.Original))
            {
                throw new InvalidOperationException(
                    $"Modellfehler: Distributionslizenz „{licensing.Distribution}“ und Originallizenz „{licensing.Original}“ lassen sich in CycloneDX 1.6 nur gemeinsam schreiben, wenn beide einzelne Kennungen sind.");
            }
            foreach (var (id, acknowledgement) in new[] { (licensing.Distribution, "concluded"), (licensing.Original, "declared") })
            {
                w.WriteStartObject();
                w.WriteStartObject("license");
                w.WriteString(SpdxLicense.IsListed(id) ? "id" : "name", id);
                w.WriteString("acknowledgement", acknowledgement);
                w.WriteEndObject();
                w.WriteEndObject();
            }
        }
        w.WriteEndArray();
    }

    private static void WriteProperties(Utf8JsonWriter w, IEnumerable<KeyValuePair<string, string>> properties)
    {
        var sorted = properties
            .Distinct()
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ThenBy(p => p.Value, StringComparer.Ordinal)
            .ToList();
        if (sorted.Count == 0)
        {
            return;
        }
        w.WriteStartArray("properties");
        foreach (var (name, value) in sorted)
        {
            w.WriteStartObject();
            w.WriteString("name", name);
            w.WriteString("value", value);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
}
