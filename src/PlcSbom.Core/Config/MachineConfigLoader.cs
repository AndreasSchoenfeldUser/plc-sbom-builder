using System.Text.RegularExpressions;
using PlcSbom.Core.Adapters.TiaAml;
using PlcSbom.Core.Model;
using PlcSbom.Core.Output;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace PlcSbom.Core.Config;

/// <summary>
/// Reads machine.yml strictly (CLAUDE.md section 7.4): unknown keys, wrong types and keys that do not
/// apply to the station's adapter are errors (exit 2), so typing mistakes are not silently ignored.
/// Values are read as text exactly as written; YAML does not turn "1.10" into a number here.
/// </summary>
public static partial class MachineConfigLoader
{
    public const string DefaultNamespace = "plcsbom";

    private static readonly string[] TopKeys =
    [
        "machine", "namespace", "gsdml", "deviceDescriptions", "passiveOrderNumbers", "vendorOverrides",
        "protectedBlockVendors", "vendorContacts", "licenses", "sourceUrl", "stations", "vendorSboms",
        "partialSboms", "cpeMap",
    ];

    private static readonly string[] MachineKeys = ["name", "supplier", "contact"];

    private static readonly string[] StationKeys =
    [
        "name", "adapter", "source", "path", "plc", "program", "devices", "ignoreDevices", "libraries",
        "allowUpgrade", "globalLibraries",
    ];

    /// <summary>Station keys that only some adapters understand.</summary>
    private static readonly Dictionary<string, string[]> AdapterKeys = new(StringComparer.Ordinal)
    {
        ["source"] = [AdapterIds.Codesys],
        ["plc"] = [AdapterIds.Codesys, AdapterIds.TiaAml, AdapterIds.TiaPortal],
        ["devices"] = [AdapterIds.TiaAml, AdapterIds.TiaPortal],
        ["ignoreDevices"] = [AdapterIds.TiaAml, AdapterIds.TiaPortal],
        ["libraries"] = [AdapterIds.TiaAml],
        ["allowUpgrade"] = [AdapterIds.TiaPortal],
        ["globalLibraries"] = [AdapterIds.TiaPortal],
    };

    public static readonly string[] CodesysSources = ["v35", "v23", "c4"];

    public static MachineConfig Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new UsageException("CONFIG_NOT_FOUND", $"Die Maschinenbeschreibung {path} gibt es nicht.");
        }
        string text;
        try
        {
            text = File.ReadAllText(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UsageException("CONFIG_NOT_READABLE", $"Die Maschinenbeschreibung {path} ist nicht lesbar: {ex.Message}", ex);
        }
        return Parse(text, fullPath, path);
    }

    /// <summary>Parses the text of machine.yml; <paramref name="displayPath"/> names the file in messages.</summary>
    public static MachineConfig Parse(string text, string fullPath, string displayPath)
    {
        var r = new Reader(displayPath);
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException ex)
        {
            throw r.Error(ex.Start, $"Kein gültiges YAML: {ex.Message}", ex);
        }
        if (stream.Documents.Count != 1)
        {
            throw r.Error(null, stream.Documents.Count == 0 ? "Die Datei ist leer." : "Die Datei enthält mehrere YAML-Dokumente, erwartet ist eines.");
        }
        var root = r.Mapping(stream.Documents[0].RootNode, "Die Maschinenbeschreibung");
        r.Keys(root, TopKeys, "oberster Ebene");

        var machine = r.Mapping(r.Required(root, "machine", "der Maschinenbeschreibung"), "„machine“");
        r.Keys(machine, MachineKeys, "„machine“");
        var machineName = r.RequiredText(machine, "name", "„machine“");
        var supplier = r.RequiredText(machine, "supplier", "„machine“");
        var contact = r.OptionalText(machine, "contact");
        if (contact is not null)
        {
            _ = r.Contact(machine.Children[new YamlScalarNode("contact")], contact, "machine.contact");
        }

        var ns = r.OptionalText(root, "namespace") ?? DefaultNamespace;
        if (!NamespaceRegex().IsMatch(ns) || ns.Equals("cdx", StringComparison.OrdinalIgnoreCase) || ns.Equals("bsi", StringComparison.OrdinalIgnoreCase))
        {
            throw r.Error(root.Children[new YamlScalarNode("namespace")].Start,
                $"Der Namespace „{ns}“ ist nicht erlaubt. Erlaubt sind Buchstaben, Ziffern, „.“, „_“ und „-“, nicht „cdx“ und „bsi“ (beide sind vergeben).");
        }

        var stationsNode = r.Sequence(r.Required(root, "stations", "der Maschinenbeschreibung"), "„stations“");
        if (stationsNode.Children.Count == 0)
        {
            throw r.Error(stationsNode.Start, "„stations“ ist leer; eine Maschine braucht mindestens eine Station.");
        }
        var stations = stationsNode.Children.Select(n => Station(r, n)).ToList();
        foreach (var duplicate in stations.GroupBy(s => s.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            throw r.Error(stationsNode.Start, $"Der Stationsname „{duplicate.Key}“ kommt mehrfach vor. Stationsnamen müssen eindeutig sein.");
        }

        var licenses = r.Map(root, "licenses");
        foreach (var (vendor, licence) in licenses)
        {
            if (!SpdxLicense.IsValidExpression(licence))
            {
                throw r.Error(r.ValueNode(root, "licenses", vendor)?.Start,
                    $"Die Lizenz „{licence}“ für {vendor} unter „licenses“ ist weder eine SPDX-Kennung noch ein SPDX-Ausdruck noch eine Kennung LicenseRef-… (TR-03183-2 Abschnitt 6.1).");
            }
        }
        var contacts = r.Map(root, "vendorContacts");
        foreach (var (vendor, value) in contacts)
        {
            _ = r.Contact(r.ValueNode(root, "vendorContacts", vendor), value, $"vendorContacts.{vendor}");
        }

        var sourceUrl = r.OptionalText(root, "sourceUrl");
        if (sourceUrl is not null && (!sourceUrl.Contains("://", StringComparison.Ordinal) || !sourceUrl.Contains("{commit}", StringComparison.Ordinal) || !sourceUrl.Contains("{path}", StringComparison.Ordinal)))
        {
            throw r.Error(root.Children[new YamlScalarNode("sourceUrl")].Start,
                $"„sourceUrl“ muss eine URL mit den Platzhaltern {{commit}} und {{path}} sein, etwa https://git.example/maschine/-/tree/{{commit}}/{{path}}. Gefunden: „{sourceUrl}“.");
        }

        var partials = new List<PartialSbomConfig>();
        if (root.Children.TryGetValue(new YamlScalarNode("partialSboms"), out var partialNode))
        {
            foreach (var item in r.Sequence(partialNode, "„partialSboms“").Children)
            {
                var entry = r.Mapping(item, "Ein Eintrag unter „partialSboms“");
                r.Keys(entry, ["path", "station"], "„partialSboms“");
                var station = r.OptionalText(entry, "station");
                if (station is not null && stations.All(s => s.Name != station))
                {
                    throw r.Error(entry.Start, $"Die Teil-SBOM nennt die Station „{station}“, die es unter „stations“ nicht gibt. Vorhanden: {string.Join(", ", stations.Select(s => s.Name))}.");
                }
                partials.Add(new PartialSbomConfig(r.RequiredText(entry, "path", "Ein Eintrag unter „partialSboms“"), station));
            }
        }

        return new MachineConfig
        {
            FullPath = fullPath,
            Machine = new MachineSection(machineName, supplier, contact),
            Namespace = ns,
            Gsdml = r.TextList(root, "gsdml"),
            DeviceDescriptions = r.TextList(root, "deviceDescriptions"),
            PassiveOrderNumbers = r.TextList(root, "passiveOrderNumbers"),
            VendorOverrides = r.Map(root, "vendorOverrides"),
            ProtectedBlockVendors = r.Map(root, "protectedBlockVendors"),
            VendorContacts = contacts,
            Licenses = licenses,
            SourceUrl = sourceUrl,
            Stations = stations,
            VendorSboms = r.TextList(root, "vendorSboms", allowSingle: true),
            PartialSboms = partials,
            CpeMap = r.OptionalText(root, "cpeMap"),
        };
    }

    /// <summary>
    /// Contact of machine.yml as party data: an email address (also with mailto:) or an http(s) URL.
    /// TR 5.2.1/5.2.2 ask for the email address and accept a URL only instead.
    /// </summary>
    public static Party ToParty(string name, string? contact)
    {
        if (contact is null)
        {
            return new Party(name);
        }
        var text = contact.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? contact[7..] : contact;
        return IsUrl(text) ? new Party(name, Url: text) : new Party(name, Email: text);
    }

    private static bool IsUrl(string text) =>
        text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    private static StationConfig Station(Reader r, YamlNode node)
    {
        var map = r.Mapping(node, "Ein Eintrag unter „stations“");
        r.Keys(map, StationKeys, "einer Station");
        var name = r.RequiredText(map, "name", "Eine Station");
        var where = $"der Station {name}";
        var adapter = r.RequiredText(map, "adapter", where);
        if (!AdapterIds.All.Contains(adapter, StringComparer.Ordinal))
        {
            throw r.Error(map.Children[new YamlScalarNode("adapter")].Start,
                $"Unbekannter Adapter „{adapter}“ an {where}. Bekannt sind: {string.Join(", ", AdapterIds.All)}.");
        }
        foreach (var (key, adapters) in AdapterKeys)
        {
            if (map.Children.ContainsKey(new YamlScalarNode(key)) && !adapters.Contains(adapter, StringComparer.Ordinal))
            {
                throw r.Error(map.Children[new YamlScalarNode(key)].Start,
                    $"Der Schlüssel „{key}“ gilt nicht für den Adapter {adapter} ({where}); er gilt für {string.Join(", ", adapters)}.");
            }
        }
        var source = r.OptionalText(map, "source");
        if (adapter == AdapterIds.Codesys)
        {
            if (source is null)
            {
                throw r.Error(map.Start, $"„source“ fehlt an {where}. Der Adapter codesys braucht die Quellvariante: {string.Join(", ", CodesysSources)}.");
            }
            if (!CodesysSources.Contains(source, StringComparer.Ordinal))
            {
                throw r.Error(map.Children[new YamlScalarNode("source")].Start,
                    $"Unbekannte Quellvariante „{source}“ an {where}. Bekannt sind: {string.Join(", ", CodesysSources)}.");
            }
        }

        var libraries = new List<DeclaredLibrary>();
        if (map.Children.TryGetValue(new YamlScalarNode("libraries"), out var librariesNode))
        {
            foreach (var item in r.Sequence(librariesNode, $"„libraries“ {where}").Children)
            {
                var library = r.Mapping(item, $"Ein Eintrag unter „libraries“ {where}");
                r.Keys(library, ["name", "vendor", "version"], $"„libraries“ {where}");
                libraries.Add(new DeclaredLibrary(
                    r.RequiredText(library, "name", $"Ein Eintrag unter „libraries“ {where}"),
                    r.OptionalText(library, "vendor"),
                    r.OptionalText(library, "version")));
            }
        }

        return new StationConfig
        {
            Name = name,
            Adapter = adapter,
            Source = source,
            Path = r.RequiredText(map, "path", where),
            Plc = r.OptionalText(map, "plc"),
            Program = r.OptionalText(map, "program"),
            Devices = r.TextList(map, "devices"),
            IgnoreDevices = r.TextList(map, "ignoreDevices"),
            Libraries = libraries,
            AllowUpgrade = r.OptionalBool(map, "allowUpgrade") ?? false,
            GlobalLibraries = r.TextList(map, "globalLibraries"),
        };
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex NamespaceRegex();

    /// <summary>Typed access to YAML nodes with messages that name file and line.</summary>
    private sealed class Reader(string displayPath)
    {
        public UsageException Error(Mark? at, string message, Exception? inner = null) =>
            new("CONFIG_INVALID", at is { } mark && mark.Line > 0 ? $"{displayPath}, Zeile {mark.Line}: {message}" : $"{displayPath}: {message}", inner);

        public UsageException Error(Mark at, string message, Exception? inner = null) => Error((Mark?)at, message, inner);

        public YamlMappingNode Mapping(YamlNode node, string what) =>
            node as YamlMappingNode ?? throw Error(node.Start, $"{what} muss eine Zuordnung aus Schlüsseln und Werten sein.");

        public YamlSequenceNode Sequence(YamlNode node, string what) =>
            node as YamlSequenceNode ?? throw Error(node.Start, $"{what} muss eine Liste sein.");

        public void Keys(YamlMappingNode map, string[] allowed, string where)
        {
            foreach (var key in map.Children.Keys)
            {
                var name = (key as YamlScalarNode)?.Value;
                if (name is null || !allowed.Contains(name, StringComparer.Ordinal))
                {
                    throw Error(key.Start, $"Unbekannter Schlüssel „{name ?? key.ToString()}“ auf {where}. Erlaubt sind: {string.Join(", ", allowed)}.");
                }
            }
        }

        public YamlNode Required(YamlMappingNode map, string key, string where) =>
            map.Children.TryGetValue(new YamlScalarNode(key), out var node) && !IsNull(node)
                ? node
                : throw Error(map.Start, $"„{key}“ fehlt in {where}.");

        public string RequiredText(YamlMappingNode map, string key, string where) =>
            OptionalText(map, key) ?? throw Error(map.Start, $"„{key}“ fehlt in {where}.");

        public string? OptionalText(YamlMappingNode map, string key)
        {
            if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node) || IsNull(node))
            {
                return null;
            }
            var value = Text(node, $"„{key}“");
            return value.Trim().Length == 0 ? null : value.Trim();
        }

        public bool? OptionalBool(YamlMappingNode map, string key)
        {
            var text = OptionalText(map, key);
            return text switch
            {
                null => null,
                "true" => true,
                "false" => false,
                _ => throw Error(map.Children[new YamlScalarNode(key)].Start, $"„{key}“ erwartet true oder false, nicht „{text}“."),
            };
        }

        /// <summary>A list of texts; with <paramref name="allowSingle"/> also a single text.</summary>
        public IReadOnlyList<string> TextList(YamlMappingNode map, string key, bool allowSingle = false)
        {
            if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node) || IsNull(node))
            {
                return [];
            }
            if (allowSingle && node is YamlScalarNode)
            {
                return [Text(node, $"„{key}“").Trim()];
            }
            return Sequence(node, $"„{key}“").Children.Select(n => Text(n, $"Ein Eintrag unter „{key}“").Trim()).ToList();
        }

        /// <summary>A mapping of texts in file order (order matters for vendorOverrides).</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Map(YamlMappingNode map, string key)
        {
            if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node) || IsNull(node))
            {
                return [];
            }
            return Mapping(node, $"„{key}“").Children
                .Select(c => new KeyValuePair<string, string>(Text(c.Key, $"Ein Schlüssel unter „{key}“").Trim(), Text(c.Value, $"Ein Wert unter „{key}“").Trim()))
                .ToList();
        }

        public YamlNode? ValueNode(YamlMappingNode map, string key, string entry) =>
            map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlMappingNode m
                ? m.Children.FirstOrDefault(c => (c.Key as YamlScalarNode)?.Value?.Trim() == entry).Value
                : null;

        public Party Contact(YamlNode? node, string value, string where)
        {
            var text = value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? value[7..] : value;
            var isEmail = !text.Contains("://", StringComparison.Ordinal) && text.IndexOf('@') > 0 && !text.Contains(' ', StringComparison.Ordinal);
            if (!isEmail && !IsUrl(text))
            {
                throw Error(node?.Start, $"„{where}“ erwartet eine E-Mail-Adresse oder eine URL mit http:// oder https://, nicht „{value}“.");
            }
            return ToParty("", value);
        }

        private string Text(YamlNode node, string what) =>
            node is YamlScalarNode scalar
                ? scalar.Value ?? ""
                : throw Error(node.Start, $"{what} muss ein einzelner Wert sein, keine Liste oder Zuordnung.");

        private static bool IsNull(YamlNode node) =>
            node is YamlScalarNode { Style: ScalarStyle.Plain, Value: null or "" or "~" or "null" };
    }
}
