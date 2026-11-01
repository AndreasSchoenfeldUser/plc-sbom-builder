using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlcSbom.Core.Sbom;

/// <summary>
/// Read-only view of a finished CycloneDX SBOM for report, compare and ids (M10 to M12). Works on the
/// JSON, like the validator, so it reads own and foreign SBOMs the same way. Properties of plc-sbom are
/// found through the namespace of the file (<see cref="Namespace"/>); a foreign SBOM without them still
/// gives components, versions and dependencies.
/// </summary>
public sealed class SbomView
{
    private readonly Dictionary<string, SbomComponent> byRef;

    private SbomView(JsonObject root, string? ns, SbomComponent? primary, IReadOnlyList<SbomComponent> components)
    {
        Root = root;
        Namespace = ns;
        Primary = primary;
        Components = components;
        byRef = new Dictionary<string, SbomComponent>(StringComparer.Ordinal);
        foreach (var component in components.Prepend(primary).OfType<SbomComponent>())
        {
            if (component.BomRef is { } bomRef)
            {
                byRef.TryAdd(bomRef, component);
            }
        }

        var dependsOn = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var entry in (root["dependencies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(entry["ref"]) is { } from)
            {
                dependsOn[from] = (entry["dependsOn"] as JsonArray ?? []).Select(Text).OfType<string>().ToList();
            }
        }
        DependsOn = dependsOn;

        var completeness = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var composition in (root["compositions"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(composition["aggregate"]) is { } aggregate)
            {
                foreach (var member in (composition["dependencies"] as JsonArray ?? []).Select(Text).OfType<string>())
                {
                    completeness.TryAdd(member, aggregate);
                }
            }
        }
        Completeness = completeness;
    }

    /// <summary>The parsed document.</summary>
    public JsonObject Root { get; }

    /// <summary>Property namespace of plc-sbom in this file (machine.yml "namespace"), null for a foreign SBOM.</summary>
    public string? Namespace { get; }

    /// <summary>metadata.component: the machine.</summary>
    public SbomComponent? Primary { get; }

    /// <summary>Every component below "components", nested ones included, in document order.</summary>
    public IReadOnlyList<SbomComponent> Components { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DependsOn { get; }

    /// <summary>Aggregate of "compositions" per bom-ref (complete, incomplete, unknown …).</summary>
    public IReadOnlyDictionary<string, string> Completeness { get; }

    public string? SpecVersion => Text(Root["specVersion"]);

    public string? SerialNumber => Text(Root["serialNumber"]);

    public DateTimeOffset? Timestamp =>
        Text(Root["metadata"]?["timestamp"]) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : null;

    /// <summary>metadata.manufacturer: creator of the SBOM (TR 5.2.1).</summary>
    public SbomParty? Creator => SbomParty.From(Root["metadata"]?["manufacturer"] as JsonObject);

    /// <summary>metadata.tools.components (CycloneDX 1.5+) or the legacy tools array.</summary>
    public IReadOnlyList<SbomTool> Tools
    {
        get
        {
            var tools = Root["metadata"]?["tools"];
            var list = tools is JsonObject o ? o["components"] as JsonArray : tools as JsonArray;
            return (list ?? []).OfType<JsonObject>()
                .Select(t => new SbomTool(
                    Text(t["name"]) ?? "",
                    Text(t["version"]),
                    Text(t["manufacturer"]?["name"]) ?? Text(t["vendor"]),
                    Namespace is null ? null : Properties(t).FirstOrDefault(p => p.Key == $"{Namespace}:tool:release").Value))
                .ToList();
        }
    }

    public SbomComponent? Find(string bomRef) => byRef.GetValueOrDefault(bomRef);

    /// <summary>Parses JSON text. Not JSON or not an object: <see cref="JsonException"/>.</summary>
    public static SbomView Parse(string json) =>
        From(JsonNode.Parse(json) as JsonObject ?? throw new JsonException("Kein JSON-Objekt."));

    public static SbomView From(JsonObject root)
    {
        var primaryJson = root["metadata"]?["component"] as JsonObject;
        var ns = DetectNamespace(root, primaryJson);
        var primary = primaryJson is null ? null : new SbomComponent(primaryJson, ns, null);
        var components = new List<SbomComponent>();
        void Collect(JsonArray? array, SbomComponent? parent)
        {
            foreach (var json in (array ?? []).OfType<JsonObject>())
            {
                var component = new SbomComponent(json, ns, parent);
                components.Add(component);
                Collect(json["components"] as JsonArray, component);
            }
        }
        Collect(root["components"] as JsonArray, null);
        return new SbomView(root, ns, primary, components);
    }

    /// <summary>
    /// The namespace in which plc-sbom wrote its properties: the one of "&lt;ns&gt;:logicalComponent" at the
    /// machine, otherwise of the first "&lt;ns&gt;:station". "cdx" and "bsi" are never it.
    /// </summary>
    private static string? DetectNamespace(JsonObject root, JsonObject? primary)
    {
        static string? Prefix(string name, string suffix) =>
            name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length
            && name[..^suffix.Length] is var ns && ns is not "cdx" and not "bsi" && !ns.Contains(':', StringComparison.Ordinal)
                ? ns
                : null;

        if (primary is not null)
        {
            foreach (var (name, _) in Properties(primary))
            {
                if (Prefix(name, ":logicalComponent") is { } ns)
                {
                    return ns;
                }
            }
        }
        IEnumerable<JsonObject> All(JsonArray? array) =>
            (array ?? []).OfType<JsonObject>().SelectMany(c => All(c["components"] as JsonArray).Prepend(c));
        foreach (var component in All(root["components"] as JsonArray))
        {
            foreach (var (name, _) in Properties(component))
            {
                if (Prefix(name, ":station") is { } ns)
                {
                    return ns;
                }
            }
        }
        return null;
    }

    internal static IEnumerable<KeyValuePair<string, string>> Properties(JsonObject component) =>
        (component["properties"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(p => (Name: Text(p["name"]), Value: Text(p["value"])))
            .Where(p => p.Name is not null && p.Value is not null)
            .Select(p => new KeyValuePair<string, string>(p.Name!, p.Value!));

    internal static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

/// <summary>One component of an SBOM with its properties.</summary>
public sealed class SbomComponent
{
    private readonly string? ns;

    internal SbomComponent(JsonObject json, string? ns, SbomComponent? parent)
    {
        Json = json;
        this.ns = ns;
        Parent = parent;
        Properties = SbomView.Properties(json).ToList();
    }

    public JsonObject Json { get; }

    /// <summary>Component this one is nested in ("components" of a component), null at top level.</summary>
    public SbomComponent? Parent { get; }

    public IReadOnlyList<KeyValuePair<string, string>> Properties { get; }

    public string? BomRef => SbomView.Text(Json["bom-ref"]);

    public string Type => SbomView.Text(Json["type"]) ?? "";

    public string Name => SbomView.Text(Json["name"]) ?? "";

    public string? Version => SbomView.Text(Json["version"]);

    public string? Cpe => SbomView.Text(Json["cpe"]);

    public string? Purl => SbomView.Text(Json["purl"]);

    /// <summary>Component creator: supplier, otherwise manufacturer; the contact of either.</summary>
    public SbomParty? Supplier
    {
        get
        {
            var supplier = SbomParty.From(Json["supplier"] as JsonObject);
            var manufacturer = SbomParty.From(Json["manufacturer"] as JsonObject);
            return supplier is null || manufacturer is null
                ? supplier ?? manufacturer
                : supplier with { Email = supplier.Email ?? manufacturer.Email, Url = supplier.Url ?? manufacturer.Url };
        }
    }

    /// <summary>First value of a property with the full name, e.g. "bsi:component:filename".</summary>
    public string? Property(string name) => Properties.FirstOrDefault(p => p.Key == name).Value;

    /// <summary>First value of "&lt;ns&gt;:&lt;key&gt;", null without namespace.</summary>
    public string? Own(string key) => ns is null ? null : Property($"{ns}:{key}");

    /// <summary>All values of "&lt;ns&gt;:&lt;key&gt;".</summary>
    public IEnumerable<string> OwnAll(string key) =>
        ns is null ? [] : Properties.Where(p => p.Key == $"{ns}:{key}").Select(p => p.Value);

    public string? Station => Own("station");

    /// <summary>Gap codes recorded at the component ("&lt;ns&gt;:gap").</summary>
    public IReadOnlyList<string> Gaps => OwnAll("gap").Distinct(StringComparer.Ordinal).ToList();

    /// <summary>A firmware component that describes a device (order number or position from an adapter).</summary>
    public bool IsDevice => Type == "firmware" && (Own("device:orderNumber") is not null || Own("device:position") is not null || Own("device:type") is not null);

    /// <summary>"&lt;ns&gt;:software:kind", e.g. user-program, library, runtime.</summary>
    public string? SoftwareKind => Own("software:kind");

    /// <summary>Source file of a component taken from a vendor or partial SBOM.</summary>
    public string? InputSbom => Own("inputSbom");

    /// <summary>SHA-512 of the deployable form (externalReferences of type distribution).</summary>
    public bool HasDistributionHash =>
        (Json["externalReferences"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(r => SbomView.Text(r["type"]) == "distribution")
            .SelectMany(r => (r["hashes"] as JsonArray ?? []).OfType<JsonObject>())
            .Any(h => SbomView.Text(h["alg"]) == "SHA-512" && SbomView.Text(h["content"]) is { Length: > 0 });
}

/// <summary>Organisation in an SBOM with contact.</summary>
public sealed record SbomParty(string Name, string? Email, string? Url)
{
    internal static SbomParty? From(JsonObject? entity)
    {
        if (entity is null || SbomView.Text(entity["name"]) is not { } name)
        {
            return null;
        }
        var url = (entity["url"] as JsonArray ?? []).Select(SbomView.Text).OfType<string>().FirstOrDefault();
        var email = (entity["contact"] as JsonArray ?? []).OfType<JsonObject>().Select(c => SbomView.Text(c["email"])).OfType<string>().FirstOrDefault();
        return new SbomParty(name, email, url);
    }
}

/// <summary>Entry of metadata.tools: a tool that wrote the SBOM or an engineering tool of a source.</summary>
public sealed record SbomTool(string Name, string? Version, string? Vendor, string? Release);
