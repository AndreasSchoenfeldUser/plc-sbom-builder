using System.Globalization;
using System.Security.Cryptography;
using PlcSbom.Core.Adapters.Gsdml;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;

namespace PlcSbom.Core.Adapters.TiaAml;

/// <summary>A library the user declares for a station, because the AML export contains none (machine.yml "libraries").</summary>
public sealed record DeclaredLibrary(string Name, string? Vendor = null, string? Version = null);

/// <summary>One station of machine.yml that reads this AML file (CLAUDE.md section 7.4).</summary>
public sealed record TiaAmlStationOptions
{
    /// <summary>Station name in the SBOM.</summary>
    public required string StationName { get; init; }

    /// <summary>Element name of the CPU in the AML. Without it the whole file is the station.</summary>
    public string? Plc { get; init; }

    /// <summary>Further devices without IO system, matched by device or device item name ('*', '?').</summary>
    public IReadOnlyList<string> Devices { get; init; } = [];

    /// <summary>Devices of the file no station claims and that are deliberately left out.</summary>
    public IReadOnlyList<string> IgnoreDevices { get; init; } = [];

    /// <summary>Declared libraries; without them the station carries libraries-not-read.</summary>
    public IReadOnlyList<DeclaredLibrary> Libraries { get; init; } = [];
}

/// <summary>What the caller (later machine.yml) tells the adapter about one AML file and the stations reading it.</summary>
public sealed record TiaAmlOptions
{
    /// <summary>Source file relative to the repository, with '/', as written into the SBOM.</summary>
    public required string SourcePath { get; init; }

    /// <summary>All stations that point to this file; the file is read once.</summary>
    public required IReadOnlyList<TiaAmlStationOptions> Stations { get; init; }

    /// <summary>machine.yml "passiveOrderNumbers": devices without firmware attribute declared passive.</summary>
    public IReadOnlyList<string> PassiveOrderNumbers { get; init; } = [];

    /// <summary>machine.yml "vendorOverrides": order number pattern → vendor, first match wins.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> VendorOverrides { get; init; } = [];

    /// <summary>GSDML files from machine.yml "gsdml". Null: no directory given, GSD devices carry gsdml-not-found.</summary>
    public GsdmlIndex? Gsdml { get; init; }
}

/// <summary>Stations of one AML file, devices that belong to none, devices left out on purpose, and warnings.</summary>
public sealed record TiaAmlResult(
    IReadOnlyList<Station> Stations,
    IReadOnlyList<Device> UnassignedDevices,
    IReadOnlyList<Device> IgnoredDevices,
    IReadOnlyList<AdapterWarning> Warnings);

/// <summary>How the adapter treats a device item (shown by inspect).</summary>
public enum ItemTreatment
{
    /// <summary>Firmware component with projected version.</summary>
    Firmware,

    /// <summary>Firmware component without version: the item has no FirmwareVersion attribute.</summary>
    FirmwareAttributeMissing,

    /// <summary>FirmwareVersion V0.0 or declared passive: hardware appendix, not in the SBOM.</summary>
    Passive,

    /// <summary>Rack or profile rail with order number: hardware appendix.</summary>
    Rack,

    /// <summary>Head module of a GSD device: firmware component without order number, vendor and version.</summary>
    GsdDevice,

    /// <summary>Module of a GSD device without own firmware: hardware appendix.</summary>
    GsdModule,

    /// <summary>System: device or rack, GSD device or rack, item without TypeIdentifier: structure only.</summary>
    Structure,

    /// <summary>Unknown TypeIdentifier prefix or GSD kind: not evaluated, warning.</summary>
    Unknown,
}

/// <summary>Raw inventory with the default assignment per CPU (plc-sbom inspect --adapter tia-aml).</summary>
public sealed record TiaAmlInspection(
    string? ProjectName,
    ToolInfo? Tool,
    DateTimeOffset? ExportedAt,
    IReadOnlyList<TiaAmlInspection.Cpu> Cpus,
    IReadOnlyList<TiaAmlInspection.DeviceEntry> Devices,
    IReadOnlyList<AdapterWarning> Warnings)
{
    public sealed record Cpu(string Name, string Device, string? TypeName, string? FirmwareVersion, IReadOnlyList<string> IoSystems);

    /// <summary>A device and the CPUs whose station it would belong to: "cpu" (contains it) or "io-system" (linked).</summary>
    public sealed record DeviceEntry(string Name, string? Folder, string? TypeIdentifier, IReadOnlyList<Assignment> AssignedTo, IReadOnlyList<Item> Items);

    public sealed record Assignment(string Cpu, string Via);

    public sealed record Item(string Name, string Position, string? TypeIdentifier, string? TypeName, string? FirmwareVersion, string? DeviceItemType, ItemTreatment Treatment, GsdmlFacts? Gsdml);

    /// <summary>What the GSDML file says about a GSD item (only with --gsdml and a file found).</summary>
    public sealed record GsdmlFacts(string File, string? Vendor, string? OrderNumber, string? SoftwareRelease, string? HardwareRelease);
}

/// <summary>
/// Adapter tia-aml (CLAUDE.md section 5.4): AutomationML file from the CAx export of TIA Portal V17–V21.
/// A station is a CPU and the devices of its IO systems. Field table and rules in docs/adapter-tia-aml.md.
/// </summary>
public static class TiaAmlAdapter
{
    public const string WriterName = "Totally Integrated Automation Portal";

    public const string ToolName = "TIA Portal";

    /// <summary>Vendor of OrderNumber: devices (Siemens hardware catalogue and HSPs), an assumption (E-019).</summary>
    public const string SiemensName = "Siemens AG";

    /// <summary>WriterVersion → TIA Portal version, as found in the fixtures (1700, 1800, 2100) and their pattern.</summary>
    private static readonly Dictionary<string, string> TiaVersions = new(StringComparer.Ordinal)
    {
        ["1700"] = "V17",
        ["1800"] = "V18",
        ["1900"] = "V19",
        ["2000"] = "V20",
        ["2100"] = "V21",
    };

    private const string OrderNumberPrefix = "OrderNumber:";
    private const string SystemPrefix = "System:";
    private const string GsdPrefix = "GSD:";

    public static byte[] ReadFile(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new UsageException("SOURCE_NOT_FOUND", $"Die Quelldatei {fullPath} gibt es nicht.");
        }
        try
        {
            return File.ReadAllBytes(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UsageException("SOURCE_NOT_READABLE", $"Die Quelldatei {fullPath} ist nicht lesbar: {ex.Message}", ex);
        }
    }

    public static TiaAmlResult Read(string fullPath, TiaAmlOptions options)
    {
        var content = ReadFile(fullPath);
        var project = AmlReader.Read(content, options.SourcePath);
        return ToStations(project, Convert.ToHexStringLower(SHA256.HashData(content)), options);
    }

    // ---- File level: writer, version, warnings ----------------------------------------------------

    /// <summary>Checks the writer and returns the TIA Portal version for metadata.tools (CLAUDE.md 5.4.1).</summary>
    public static ToolInfo? Tool(AmlProject project, string displayPath, List<AdapterWarning> warnings)
    {
        var writer = project.Writer;
        if (writer.Name != WriterName)
        {
            throw new UsageException(
                "NOT_TIA_AML",
                writer.Name is null
                    ? $"Die Datei {displayPath} ist keine AML-Datei aus TIA Portal: Sie hat keinen WriterHeader mit WriterName."
                    : $"Die Datei {displayPath} ist keine AML-Datei aus TIA Portal: WriterName ist „{writer.Name}“ statt „{WriterName}“.");
        }
        if (writer.Version is null)
        {
            warnings.Add(new AdapterWarning("TIA_AML_WRITER_VERSION_UNKNOWN", $"{displayPath} nennt keine WriterVersion. Die TIA-Version fehlt in metadata.tools."));
            return null;
        }
        if (!TiaVersions.TryGetValue(writer.Version, out var version))
        {
            warnings.Add(new AdapterWarning(
                "TIA_AML_WRITER_VERSION_UNKNOWN",
                $"{displayPath} stammt von TIA Portal mit WriterVersion {writer.Version}. Geprüft sind die Exporte aus V17 bis V21 (1700 bis 2100); die Datei wird trotzdem gelesen."));
            version = writer.Version;
        }
        return new ToolInfo(ToolName, version, writer.Release, writer.Vendor);
    }

    /// <summary>LastWritingDateTime, e.g. 2026-02-03T18:18:28.2456799Z; without zone read as UTC.</summary>
    public static DateTimeOffset? ExportedAt(AmlProject project, string displayPath, List<AdapterWarning> warnings)
    {
        var text = project.Writer.LastWritingDateTime;
        if (text is null)
        {
            return null;
        }
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            return value;
        }
        warnings.Add(new AdapterWarning("TIA_AML_EXPORT_TIME_INVALID", $"{displayPath}: LastWritingDateTime „{text}“ ist kein lesbarer Zeitpunkt."));
        return null;
    }

    private static void RoleWarnings(AmlProject project, string displayPath, List<AdapterWarning> warnings)
    {
        if (project.UnknownRoles.Count > 0)
        {
            warnings.Add(new AdapterWarning(
                "TIA_AML_ROLE_UNKNOWN",
                $"{displayPath} enthält Elemente mit unbekannten Rollen ({string.Join(", ", project.UnknownRoles)}). Sie werden durchlaufen, aber selbst nicht ausgewertet."));
        }
    }

    // ---- Topology: CPUs, IO systems, links ---------------------------------------------------------

    private sealed record CpuRef(AmlDevice Device, AmlItem Item);

    private sealed record Topology(IReadOnlyList<CpuRef> Cpus, IReadOnlyDictionary<string, IReadOnlyList<AmlDevice>> LinkedByCpu);

    /// <summary>
    /// CPUs are device items with DeviceItemType CPU. An IO system belongs to the CPU it hangs under;
    /// if it hangs under another item (e.g. a communication module), to the only CPU of the same device.
    /// A device belongs to a CPU's IO system if one of its interfaces is linked to it (Link To IoSystem).
    /// </summary>
    private static Topology Analyse(AmlProject project, string displayPath, List<AdapterWarning> warnings)
    {
        var cpus = project.Devices
            .SelectMany(d => d.Items.Where(i => i.DeviceItemType == "CPU").Select(i => new CpuRef(d, i)))
            .ToList();

        var ownerOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var device in project.Devices)
        {
            var deviceCpus = device.Items.Where(i => i.DeviceItemType == "CPU").ToList();
            foreach (var io in device.IoSystems)
            {
                var cpu = io.CpuItemId ?? (deviceCpus.Count == 1 ? deviceCpus[0].Id : null);
                if (cpu is null)
                {
                    warnings.Add(new AdapterWarning(
                        "TIA_AML_IOSYSTEM_WITHOUT_CPU",
                        $"{displayPath}: Das IO-System „{io.Name}“ in {device.Name} lässt sich keiner CPU zuordnen. Die Geräte daran gehören über das IO-System zu keiner Station."));
                    continue;
                }
                ownerOf[io.Id] = cpu;
            }
        }

        var linked = new Dictionary<string, IReadOnlyList<AmlDevice>>(StringComparer.Ordinal);
        foreach (var cpu in cpus)
        {
            linked[cpu.Item.Id] = project.Devices
                .Where(d => d != cpu.Device && d.LinkedIoSystemIds.Any(io => ownerOf.GetValueOrDefault(io) == cpu.Item.Id))
                .ToList();
        }
        return new Topology(cpus, linked);
    }

    private static bool Matches(string pattern, AmlDevice device) =>
        Wildcard.IsMatch(pattern, device.Name) || device.Items.Any(i => Wildcard.IsMatch(pattern, i.Name));

    // ---- Stations (CLAUDE.md 5.4.4) -----------------------------------------------------------------

    public static TiaAmlResult ToStations(AmlProject project, string sourceSha256, TiaAmlOptions options)
    {
        if (options.Stations.Count == 0)
        {
            throw new ArgumentException("Mindestens eine Station muss die AML-Datei lesen.", nameof(options));
        }
        var path = options.SourcePath;
        var warnings = new List<AdapterWarning>();
        var tool = Tool(project, path, warnings);
        var exportedAt = ExportedAt(project, path, warnings);
        RoleWarnings(project, path, warnings);
        var topology = Analyse(project, path, warnings);
        var cpuNames = string.Join(", ", topology.Cpus.Select(c => c.Item.Name));

        var plans = new List<(TiaAmlStationOptions Station, CpuRef? Cpu, HashSet<AmlDevice> Members)>();
        var ignorePatterns = options.Stations.SelectMany(s => s.IgnoreDevices).Distinct(StringComparer.Ordinal).ToList();
        var rest = new List<AmlDevice>();

        if (options.Stations.Any(s => s.Plc is null))
        {
            if (options.Stations.Count > 1)
            {
                throw new UsageException(
                    "TIA_AML_PLC_REQUIRED",
                    $"{options.Stations.Count} Stationen lesen {path}. Dann braucht jede Station „plc“ mit dem Namen ihrer CPU. Gefunden: {(cpuNames.Length == 0 ? "keine CPU" : cpuNames)}.");
            }
            if (topology.Cpus.Count > 1)
            {
                throw new UsageException(
                    "TIA_AML_PLC_REQUIRED",
                    $"{path} enthält {topology.Cpus.Count} CPUs ({cpuNames}). Für die Station {options.Stations[0].StationName} mit „plc“ eine davon wählen.");
            }
            // No plc: the whole file is the station (CLAUDE.md 5.4.4 rule 4), minus ignoreDevices.
            var members = project.Devices.Where(d => !ignorePatterns.Any(p => Matches(p, d))).ToHashSet();
            plans.Add((options.Stations[0], topology.Cpus.SingleOrDefault(), members));
            rest.AddRange(project.Devices.Where(d => !members.Contains(d)));
        }
        else
        {
            var claimedBy = new Dictionary<AmlDevice, string>();
            foreach (var station in options.Stations)
            {
                var cpu = topology.Cpus.FirstOrDefault(c => c.Item.Name == station.Plc)
                    ?? throw new UsageException(
                        "TIA_AML_PLC_NOT_FOUND",
                        $"Die CPU „{station.Plc}“ der Station {station.StationName} steht nicht in {path}. Gefunden: {(cpuNames.Length == 0 ? "keine CPU" : cpuNames)}.");
                var members = new HashSet<AmlDevice> { cpu.Device };
                members.UnionWith(topology.LinkedByCpu[cpu.Item.Id]);
                foreach (var pattern in station.Devices)
                {
                    var matched = project.Devices.Where(d => Matches(pattern, d)).ToList();
                    if (matched.Count == 0)
                    {
                        warnings.Add(new AdapterWarning("TIA_AML_PATTERN_UNUSED", $"Das Muster „{pattern}“ unter „devices“ der Station {station.StationName} trifft kein Gerät in {path}."));
                    }
                    members.UnionWith(matched);
                }
                foreach (var device in members)
                {
                    if (claimedBy.TryGetValue(device, out var other))
                    {
                        throw new UsageException(
                            "TIA_AML_DEVICE_IN_TWO_STATIONS",
                            $"Das Gerät {device.Name} in {path} gehört zu den Stationen {other} und {station.StationName}. Ein Gerät darf nur einer Station angehören.");
                    }
                    claimedBy[device] = station.StationName;
                }
                plans.Add((station, cpu, members));
            }
            rest.AddRange(project.Devices.Where(d => !claimedBy.ContainsKey(d)));
        }

        foreach (var pattern in ignorePatterns.Where(p => !project.Devices.Any(d => Matches(p, d))))
        {
            warnings.Add(new AdapterWarning("TIA_AML_PATTERN_UNUSED", $"Das Muster „{pattern}“ unter „ignoreDevices“ trifft kein Gerät in {path}."));
        }

        var context = new Interpretation(options, warnings);
        var stations = plans.Select(plan => new Station
        {
            Name = plan.Station.StationName,
            AdapterId = AdapterIds.TiaAml,
            SourcePath = path,
            SourceSha256 = sourceSha256,
            SourceExportedAt = exportedAt,
            EngineeringTool = tool,
            // Document order of the file, so the station reads like the project tree.
            Devices = project.Devices.Where(plan.Members.Contains).SelectMany(d => context.ToDevices(d, plan.Cpu?.Item.Id)).ToList(),
            Software = plan.Station.Libraries.Select(Declared).ToList(),
            Gaps = StationGaps(plan.Station, plan.Cpu),
        }).ToList();

        var ignored = rest.Where(d => ignorePatterns.Any(p => Matches(p, d))).ToHashSet();
        var unassigned = rest
            .Where(d => !ignored.Contains(d))
            .SelectMany(d => context.ToDevices(d, controllerId: null).Select(device => Unassigned(device, d, path, sourceSha256)))
            .ToList();

        return new TiaAmlResult(
            stations,
            unassigned,
            ignored.OrderBy(project.Devices.ToList().IndexOf).SelectMany(d => context.ToDevices(d, controllerId: null)).ToList(),
            warnings);
    }

    private static IReadOnlyList<Gap> StationGaps(TiaAmlStationOptions station, CpuRef? cpu)
    {
        if (cpu is null)
        {
            return [];
        }
        var gaps = new List<Gap>();
        if (station.Libraries.Count == 0)
        {
            gaps.Add(new Gap(GapCatalog.LibrariesNotRead,
                $"Der CAx-Export von TIA Portal enthält keine Bibliotheken, und für die Station {station.StationName} sind keine deklariert („libraries“ in machine.yml)."));
        }
        gaps.Add(new Gap(GapCatalog.ProgramNotRead,
            $"Der CAx-Export von TIA Portal enthält keine Bausteine der CPU {cpu.Item.Name}; ob sie geschützte Bausteine Dritter verwendet, ist unbekannt."));
        return gaps;
    }

    private static SoftwareItem Declared(DeclaredLibrary library) => new()
    {
        Kind = SoftwareKind.Library,
        Name = library.Name,
        Vendor = library.Vendor is null ? null : new Party(library.Vendor),
        VendorOrigin = ValueOrigin.Declared,
        Version = library.Version,
        VersionResolved = library.Version is not null,
        Origin = ValueOrigin.Declared,
    };

    /// <summary>Devices outside every station keep their source as properties, the writer has no station for them.</summary>
    private static Device Unassigned(Device device, AmlDevice parent, string path, string sha256)
    {
        var properties = new SortedDictionary<string, string>(device.SourceProperties.ToDictionary(), StringComparer.Ordinal)
        {
            ["adapter"] = AdapterIds.TiaAml,
            ["source"] = path,
            ["source:sha256"] = sha256,
        };
        var gaps = device.IsPassive
            ? device.Gaps
            : [.. device.Gaps, new Gap(GapCatalog.DeviceUnassigned,
                $"{parent.Name} hängt an keinem IO-System einer Station und steht in keinem „devices“ einer Station. Die Firmware hängt direkt an der Maschine.")];
        return device with { SourceProperties = properties, Gaps = gaps };
    }

    // ---- Device items → devices (CLAUDE.md 5.4.3, 5.4.5) ------------------------------------------

    /// <summary>Parts of a TypeIdentifier: prefix and, depending on it, order number or GSDML file, kind and id.</summary>
    public sealed record TypeId(string Prefix, string? OrderNumber = null, string? CatalogVersion = null, string? GsdmlFile = null, string? GsdKind = null, string? GsdId = null);

    /// <summary>
    /// "OrderNumber:6ES7 511-1AK02-0AB0[/V2.8][//…]", "System:Rack.S71500",
    /// "GSD:GSDML-V2.41-LEUZE-BCL248I-20211213.XML/DAP/DAP". Null for a missing TypeIdentifier.
    /// </summary>
    public static TypeId? ParseTypeIdentifier(string? typeIdentifier)
    {
        if (typeIdentifier is null)
        {
            return null;
        }
        if (typeIdentifier.StartsWith(OrderNumberPrefix, StringComparison.Ordinal))
        {
            var parts = typeIdentifier[OrderNumberPrefix.Length..].Split('/');
            return new TypeId(OrderNumberPrefix, parts[0].Trim(), parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : null);
        }
        if (typeIdentifier.StartsWith(SystemPrefix, StringComparison.Ordinal))
        {
            return new TypeId(SystemPrefix);
        }
        if (typeIdentifier.StartsWith(GsdPrefix, StringComparison.Ordinal))
        {
            var parts = typeIdentifier[GsdPrefix.Length..].Split('/', 3);
            return new TypeId(GsdPrefix, GsdmlFile: parts[0], GsdKind: parts.Length > 1 ? parts[1] : null, GsdId: parts.Length > 2 ? parts[2] : null);
        }
        var colon = typeIdentifier.IndexOf(':');
        return new TypeId(colon > 0 ? typeIdentifier[..(colon + 1)] : typeIdentifier);
    }

    /// <summary>Firmware for comparisons: without leading "V" (V2.8 → 2.8, 4.7.10 stays).</summary>
    public static string NormalizeFirmware(string firmware) =>
        firmware.Trim().TrimStart('V', 'v');

    private static bool IsZero(string? firmware) => firmware is not null && NormalizeFirmware(firmware) == "0.0";

    /// <summary>Treatment of one item (CLAUDE.md 5.4.3 tables). passiveOrderNumbers only affects items without FirmwareVersion.</summary>
    public static ItemTreatment Treat(AmlItem item, IReadOnlyList<string> passiveOrderNumbers)
    {
        var type = ParseTypeIdentifier(item.TypeIdentifier);
        switch (type?.Prefix)
        {
            case null or SystemPrefix:
                return ItemTreatment.Structure;
            case OrderNumberPrefix:
                if (item.TypeName == "Rack")
                {
                    return ItemTreatment.Rack;
                }
                if (item.FirmwareVersion is null)
                {
                    return passiveOrderNumbers.Any(p => Wildcard.IsOrderNumberMatch(p, type.OrderNumber!)) ? ItemTreatment.Passive : ItemTreatment.FirmwareAttributeMissing;
                }
                return IsZero(item.FirmwareVersion) ? ItemTreatment.Passive : ItemTreatment.Firmware;
            case GsdPrefix:
                return type.GsdKind switch
                {
                    "D" or "R" => ItemTreatment.Structure,
                    "DAP" => IsZero(item.FirmwareVersion) ? ItemTreatment.Passive : ItemTreatment.GsdDevice,
                    "M" => item.FirmwareVersion is null || IsZero(item.FirmwareVersion) ? ItemTreatment.GsdModule : ItemTreatment.GsdDevice,
                    _ => ItemTreatment.Unknown,
                };
            default:
                return ItemTreatment.Unknown;
        }
    }

    private sealed class Interpretation(TiaAmlOptions options, List<AdapterWarning> warnings)
    {
        private readonly HashSet<string> warned = new(StringComparer.Ordinal);

        public IEnumerable<Device> ToDevices(AmlDevice device, string? controllerId)
        {
            foreach (var item in device.Items)
            {
                var treatment = Treat(item, options.PassiveOrderNumbers);
                var type = ParseTypeIdentifier(item.TypeIdentifier);
                switch (treatment)
                {
                    case ItemTreatment.Structure:
                        continue;
                    case ItemTreatment.Unknown:
                        Warn("TIA_AML_TYPE_IDENTIFIER_UNKNOWN",
                            $"{options.SourcePath}: {device.Name}/{item.Name} hat den TypeIdentifier „{item.TypeIdentifier}“, den der Adapter nicht kennt. Das Gerät fehlt in der SBOM.");
                        continue;
                    default:
                        yield return type!.Prefix == GsdPrefix
                            ? Gsd(device, item, type, treatment)
                            : Siemens(device, item, type, treatment, item.Id == controllerId);
                        continue;
                }
            }
        }

        private void Warn(string code, string message)
        {
            if (warned.Add(message))
            {
                warnings.Add(new AdapterWarning(code, message));
            }
        }

        private Device Siemens(AmlDevice device, AmlItem item, TypeId type, ItemTreatment treatment, bool isController)
        {
            var orderNumber = type.OrderNumber!;
            var keys = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (type.CatalogVersion is { } catalog)
            {
                keys["catalogVersion"] = catalog;
                if (item.FirmwareVersion is not null && NormalizeFirmware(catalog) != NormalizeFirmware(item.FirmwareVersion))
                {
                    Warn("TIA_AML_CATALOG_VERSION_DIFFERS",
                        $"{options.SourcePath}: {device.Name}/{item.Name} hat im TypeIdentifier die Version {catalog}, als FirmwareVersion aber {item.FirmwareVersion}. In die SBOM geht die FirmwareVersion.");
                }
            }

            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var gaps = new List<Gap>();
            string? firmware = null;
            var origin = FirmwareOrigin.NotInProject;
            var passive = treatment is ItemTreatment.Passive or ItemTreatment.Rack;
            if (treatment == ItemTreatment.Firmware)
            {
                firmware = item.FirmwareVersion;
                origin = FirmwareOrigin.Projected;
            }
            else if (treatment == ItemTreatment.FirmwareAttributeMissing)
            {
                gaps.Add(new Gap(GapCatalog.FirmwareAttributeMissing,
                    $"{item.Name} ({orderNumber}) hat im AML kein Attribut FirmwareVersion. Ob das Gerät eine eigene Firmware hat, ist offen; ein passives Gerät lässt sich mit „passiveOrderNumbers“ in machine.yml erklären."));
                gaps.Add(new Gap(GapCatalog.FirmwareNotInProject,
                    $"Das AML nennt für {item.Name} ({orderNumber}) keine Firmware-Version."));
            }
            else if (treatment == ItemTreatment.Passive && item.FirmwareVersion is null)
            {
                properties["device:passive"] = "declared";
            }

            var (vendor, vendorOrigin) = Vendor(orderNumber);
            return new Device
            {
                TypeName = item.TypeName ?? orderNumber,
                Vendor = new Party(vendor),
                VendorOrigin = vendorOrigin,
                OrderNumber = orderNumber,
                OrderNumberIsPattern = orderNumber.Contains('*', StringComparison.Ordinal),
                DeviceName = item.Name,
                Position = $"{device.Name}/{item.Position}",
                Classification = item.DeviceItemType,
                IsController = isController,
                Firmware = firmware,
                FirmwareOrigin = origin,
                IsPassive = passive,
                VendorKeys = keys,
                SourceProperties = properties,
                Gaps = gaps,
            };
        }

        /// <summary>vendorOverrides first, then Siemens AG for OrderNumber: devices (assumed, E-019).</summary>
        private (string Vendor, ValueOrigin Origin) Vendor(string orderNumber) =>
            Override(orderNumber) is { } vendor ? (vendor, ValueOrigin.Declared) : (SiemensName, ValueOrigin.Assumed);

        /// <summary>machine.yml "vendorOverrides": first pattern matching the order number.</summary>
        private string? Override(string orderNumber) =>
            options.VendorOverrides.FirstOrDefault(o => Wildcard.IsOrderNumberMatch(o.Key, orderNumber)).Value;

        /// <summary>The GSDML file and item a GSD item refers to (CLAUDE.md 5.4.5), or why there is none.</summary>
        public sealed record GsdLookup(GsdmlFile? File, GsdmlItem? Item, string? NotFound);

        /// <summary>
        /// Looks up the GSDML file named in the TypeIdentifier and in it the access point (DAP) or module (M)
        /// with the id. <paramref name="warnNotFound"/>: inspect reports a missing file as a warning, the
        /// stations record it as gap gsdml-not-found instead.
        /// </summary>
        public GsdLookup Lookup(AmlDevice device, AmlItem item, TypeId type, bool warnNotFound)
        {
            var name = type.GsdmlFile!;
            if (options.Gsdml is null)
            {
                return new GsdLookup(null, null,
                    $"Die GSDML-Datei {name} wurde nicht gesucht, weil unter „gsdml“ in machine.yml kein Verzeichnis angegeben ist.");
            }
            var lookup = options.Gsdml.Find(name);
            if (lookup.Found is not { } file)
            {
                var (code, message) = lookup.IsAmbiguous
                    ? ("GSDML_AMBIGUOUS", $"Die GSDML-Datei {name} liegt mehrfach mit verschiedenem Inhalt in den Verzeichnissen aus „gsdml“ ({string.Join(", ", lookup.Candidates)}); welche TIA verwendet hat, ist offen.")
                    : ("GSDML_NOT_FOUND", $"Die GSDML-Datei {name} liegt in keinem der Verzeichnisse aus „gsdml“.");
                if (warnNotFound)
                {
                    Warn(code, message);
                }
                return new GsdLookup(null, null, message);
            }
            var items = type.GsdKind == "DAP" ? file.AccessPoints : file.Modules;
            if (type.GsdId is null || !items.TryGetValue(type.GsdId, out var gsdItem))
            {
                Warn("TIA_AML_GSDML_ITEM_NOT_FOUND",
                    $"{options.SourcePath}: {device.Name}/{item.Name} verweist auf „{type.GsdId}“ ({type.GsdKind}), das in {file.File} nicht vorkommt. Bestellnummer und Releases fehlen; passt die GSDML-Datei zum Projekt?");
                return new GsdLookup(file, null, null);
            }
            return new GsdLookup(file, gsdItem, null);
        }

        /// <summary>
        /// GSD devices: the AML names only GSDML file, kind and id. Vendor and order number come from the GSDML
        /// file (vendor:origin gsdml). SoftwareRelease describes the GSDML, not the firmware (CLAUDE.md 5.4.5).
        /// </summary>
        private Device Gsd(AmlDevice device, AmlItem item, TypeId type, ItemTreatment treatment)
        {
            var keys = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["GSDML"] = type.GsdmlFile! };
            if (type.GsdKind is { } kind)
            {
                keys["GsdType"] = kind;
            }
            if (type.GsdId is { } id)
            {
                keys["GsdId"] = id;
            }

            var isModule = treatment is ItemTreatment.GsdModule or ItemTreatment.Passive;
            var firmware = isModule || item.FirmwareVersion is null ? null : item.FirmwareVersion;
            var lookup = Lookup(device, item, type, warnNotFound: false);
            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
            Party? vendor = null;
            var vendorOrigin = ValueOrigin.Source;
            var orderNumber = lookup.Item?.OrderNumber;
            if (lookup.File is { } file)
            {
                keys["gsdmlFile"] = file.File;
                keys["gsdmlSha256"] = file.Sha256;
                AddKey(keys, "VendorId", file.VendorId);
                AddKey(keys, "DeviceId", file.DeviceId);
                if (file.VendorName is not null)
                {
                    (vendor, vendorOrigin) = (new Party(file.VendorName), ValueOrigin.Gsdml);
                }
                AddKey(properties, "gsdml:softwareRelease", lookup.Item?.SoftwareRelease);
                AddKey(properties, "gsdml:hardwareRelease", lookup.Item?.HardwareRelease);
            }
            if (orderNumber is not null && Override(orderNumber) is { } declared)
            {
                (vendor, vendorOrigin) = (new Party(declared), ValueOrigin.Declared);
            }

            var gaps = new List<Gap>();
            if (!isModule)
            {
                if (lookup.NotFound is { } notFound)
                {
                    gaps.Add(new Gap(GapCatalog.GsdmlNotFound, notFound));
                }
                if (orderNumber is null)
                {
                    gaps.Add(new Gap(GapCatalog.OrderNumberNotInProject, lookup.File is null
                        ? $"Das AML nennt für das GSD-Gerät {item.Name} keine Bestellnummer, und die GSDML-Datei {type.GsdmlFile} liegt nicht vor."
                        : $"Weder das AML noch {lookup.File.File} nennen für das GSD-Gerät {item.Name} eine Bestellnummer."));
                }
                if (vendor is null)
                {
                    gaps.Add(new Gap(GapCatalog.SupplierUnknown, lookup.File is null
                        ? $"Das AML nennt für das GSD-Gerät {item.Name} keinen Hersteller, und die GSDML-Datei {type.GsdmlFile} liegt nicht vor."
                        : $"Weder das AML noch {lookup.File.File} nennen für das GSD-Gerät {item.Name} einen Hersteller."));
                }
                if (firmware is null)
                {
                    gaps.Add(new Gap(GapCatalog.FirmwareNotInProject,
                        $"Das AML nennt für GSD-Geräte keine Firmware. Die SoftwareRelease der GSDML-Datei beschreibt die Datei, nicht das Gerät; den Stand liefert erst der Ist-Stand."));
                }
            }

            return new Device
            {
                TypeName = item.TypeName ?? item.Name,
                Vendor = vendor,
                VendorOrigin = vendorOrigin,
                OrderNumber = orderNumber,
                OrderNumberIsPattern = orderNumber is not null && GsdmlFile.IsOrderNumberPattern(orderNumber),
                DeviceName = item.Name,
                Position = $"{device.Name}/{item.Position}",
                Classification = item.DeviceItemType,
                Firmware = firmware,
                FirmwareOrigin = firmware is null ? FirmwareOrigin.NotInProject : FirmwareOrigin.Projected,
                IsPassive = isModule,
                VendorKeys = keys,
                SourceProperties = properties,
                Gaps = gaps,
            };
        }

        private static void AddKey(SortedDictionary<string, string> target, string key, string? value)
        {
            if (value is not null)
            {
                target[key] = value;
            }
        }
    }

    // ---- inspect ------------------------------------------------------------------------------------

    /// <summary>Raw inventory with the assignment each CPU would get as a station with "plc" and no "devices".</summary>
    public static TiaAmlInspection Inspect(AmlProject project, string displayPath, GsdmlIndex? gsdml = null)
    {
        var warnings = new List<AdapterWarning>();
        // Interpreting every device collects the item warnings (unknown TypeIdentifier, catalogue version, GSDML).
        var interpretation = new Interpretation(new TiaAmlOptions { SourcePath = displayPath, Stations = [], Gsdml = gsdml }, warnings);
        var tool = Tool(project, displayPath, warnings);
        var exportedAt = ExportedAt(project, displayPath, warnings);
        RoleWarnings(project, displayPath, warnings);
        var topology = Analyse(project, displayPath, warnings);

        var cpus = topology.Cpus.Select(c => new TiaAmlInspection.Cpu(
            c.Item.Name,
            c.Device.Name,
            c.Item.TypeName,
            c.Item.FirmwareVersion,
            c.Device.IoSystems.Where(io => io.CpuItemId == c.Item.Id || (io.CpuItemId is null && c.Device.Items.Count(i => i.DeviceItemType == "CPU") == 1)).Select(io => io.Name).ToList())).ToList();

        var devices = project.Devices.Select(d => new TiaAmlInspection.DeviceEntry(
            d.Name,
            d.Folder,
            d.TypeIdentifier,
            topology.Cpus
                .Select(c => c.Device == d ? new TiaAmlInspection.Assignment(c.Item.Name, "cpu")
                    : topology.LinkedByCpu[c.Item.Id].Contains(d) ? new TiaAmlInspection.Assignment(c.Item.Name, "io-system")
                    : null)
                .OfType<TiaAmlInspection.Assignment>()
                .ToList(),
            d.Items.Select(i => new TiaAmlInspection.Item(i.Name, i.Position, i.TypeIdentifier, i.TypeName, i.FirmwareVersion, i.DeviceItemType, Treat(i, []), Facts(d, i))).ToList())).ToList();

        TiaAmlInspection.GsdmlFacts? Facts(AmlDevice device, AmlItem item)
        {
            if (gsdml is null || ParseTypeIdentifier(item.TypeIdentifier) is not { Prefix: GsdPrefix, GsdKind: "DAP" or "M" } type)
            {
                return null;
            }
            var lookup = interpretation.Lookup(device, item, type, warnNotFound: true);
            return lookup.File is { } file
                ? new TiaAmlInspection.GsdmlFacts(file.File, file.VendorName, lookup.Item?.OrderNumber, lookup.Item?.SoftwareRelease, lookup.Item?.HardwareRelease)
                : null;
        }

        foreach (var device in project.Devices)
        {
            _ = interpretation.ToDevices(device, controllerId: null).ToList();
        }

        return new TiaAmlInspection(project.ProjectName, tool, exportedAt, cpus, devices, warnings);
    }
}
