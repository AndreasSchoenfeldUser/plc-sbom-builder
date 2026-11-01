using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace PlcSbom.Core.Adapters.TiaAml;

// Raw inventory of an AutomationML file from the TIA Portal CAx export (CLAUDE.md section 5.4,
// docs/adapter-tia-aml.md). Values stay as written; interpretation happens in TiaAmlAdapter.
// Elements are recognised by their role (SupportedRoleClass), not by fixed paths. Network data
// (NetworkAddress, SubnetMask, IpProtocolSelection, PROFINET device names) and tag tables are never read.

/// <summary>WriterHeader of the file: which tool wrote it, in which version and when.</summary>
public sealed record AmlWriterHeader(string? Name, string? Vendor, string? Version, string? Release, string? LastWritingDateTime);

/// <summary>Whole file: header, project name and every device with its device items.</summary>
public sealed record AmlProject(
    string? SchemaVersion,
    AmlWriterHeader Writer,
    string? ProjectName,
    IReadOnlyList<AmlDevice> Devices,
    IReadOnlyList<string> UnknownRoles);

/// <summary>An element with role Device (station, ET 200SP, drive, GSD device).</summary>
public sealed record AmlDevice(
    [property: JsonIgnore] string Id,
    string Name,
    string? TypeIdentifier,
    string? Folder,
    IReadOnlyList<AmlItem> Items,
    IReadOnlyList<AmlIoSystem> IoSystems,
    [property: JsonIgnore] IReadOnlyList<string> LinkedIoSystemIds);

/// <summary>
/// A device item that is not built in (BuiltIn not "true"). Built-in items (integrated I/O, interfaces,
/// ports) are part of their parent and not listed. <see cref="Position"/> is the path of PositionNumbers
/// from the rack, e.g. "0/1".
/// </summary>
public sealed record AmlItem(
    [property: JsonIgnore] string Id,
    string Name,
    string Position,
    string? TypeIdentifier,
    string? TypeName,
    string? FirmwareVersion,
    string? DeviceItemType);

/// <summary>A PROFINET IO system and the device item it hangs under.</summary>
public sealed record AmlIoSystem(
    [property: JsonIgnore] string Id,
    string Name,
    [property: JsonIgnore] string? CpuItemId,
    [property: JsonIgnore] string? OwnerItemId);

/// <summary>Reads an AML file from the TIA Portal CAx export.</summary>
public static class AmlReader
{
    public const string RootElement = "CAEXFile";

    // Last part of RefRoleClassPath, as found in the fixtures (CLAUDE.md 5.4.2). NodeEthernet comes from
    // AutomationProjectConfigurationEthernetRoleClassLib and stands next to Node.
    private const string RoleProject = "AutomationProject";
    private const string RoleDevice = "Device";
    private const string RoleDeviceItem = "DeviceItem";
    private const string RoleInterface = "CommunicationInterface";
    private const string RolePort = "CommunicationPort";
    private const string RoleIoSystem = "IoSystem";
    private const string RoleFolder = "DeviceUserFolder";

    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "Node", "NodeEthernet", "Subnet", "TagTable", "TagUserFolder" };

    private static readonly string[] Known = [RoleProject, RoleDevice, RoleDeviceItem, RoleInterface, RolePort, RoleIoSystem, RoleFolder, .. Skipped];

    public static AmlProject Read(byte[] content, string displayPath)
    {
        var root = SafeXml.Load(content, displayPath).Root!;
        if (root.Name.LocalName != RootElement)
        {
            throw new UsageException(
                "NOT_AML",
                $"Die Datei {displayPath} ist keine AutomationML-Datei (Wurzelelement „{root.Name.LocalName}“ statt „{RootElement}“). Angegeben wird die AML-Datei aus dem CAx-Export von TIA Portal.");
        }

        var header = root.ElementsNamed("AdditionalInformation").Select(a => a.ElementNamed("WriterHeader")).FirstOrDefault(h => h is not null);
        var writer = new AmlWriterHeader(
            header?.ChildText("WriterName"),
            header?.ChildText("WriterVendor"),
            header?.ChildText("WriterVersion"),
            header?.ChildText("WriterRelease"),
            header?.ChildText("LastWritingDateTime"));

        var walk = new Walk();
        foreach (var hierarchy in root.ElementsNamed("InstanceHierarchy"))
        {
            walk.Children(hierarchy, new Context(null, null, null, null, ""));
        }
        walk.ResolveLinks();

        return new AmlProject(
            root.AttributeValue("SchemaVersion"),
            writer,
            walk.ProjectName,
            walk.Devices.Select(d => d.Build()).ToList(),
            walk.UnknownRoles.Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>Element names without control characters (a V18 fixture has a rack named "\t_0").</summary>
    public static string CleanName(string? name)
    {
        if (name is null)
        {
            return "";
        }
        var b = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (!char.IsControl(c))
            {
                b.Append(c);
            }
        }
        return b.ToString().Trim();
    }

    /// <summary>Value of Attribute[@Name]/Value; empty counts as missing.</summary>
    private static string? Attr(XElement element, string name) =>
        element.ElementsNamed("Attribute").FirstOrDefault(a => a.AttributeValue("Name") == name)?.ChildText("Value");

    private static List<string> Roles(XElement element) =>
        element.ElementsNamed("SupportedRoleClass")
            .Select(r => r.AttributeValue("RefRoleClassPath"))
            .OfType<string>()
            .Select(p => p[(p.LastIndexOf('/') + 1)..])
            .ToList();

    /// <summary>Position in the walk: current device, nearest listed item, nearest CPU, folder, position path.</summary>
    private sealed record Context(DeviceBuilder? Device, string? ItemId, string? CpuItemId, string? Folder, string Position);

    private sealed class DeviceBuilder(string id, string name, string? typeIdentifier, string? folder)
    {
        public string Id { get; } = id;

        public List<AmlItem> Items { get; } = [];

        public List<AmlIoSystem> IoSystems { get; } = [];

        public SortedSet<string> LinkedIoSystemIds { get; } = new(StringComparer.Ordinal);

        public AmlDevice Build() => new(Id, name, typeIdentifier, folder, Items, IoSystems, LinkedIoSystemIds.ToList());
    }

    private sealed class Walk
    {
        public string? ProjectName { get; private set; }

        public List<DeviceBuilder> Devices { get; } = [];

        public SortedSet<string> UnknownRoles { get; } = new(StringComparer.Ordinal);

        /// <summary>Every element id inside a device, so link ends can be traced back to their device.</summary>
        private readonly Dictionary<string, DeviceBuilder> owner = new(StringComparer.Ordinal);

        private readonly HashSet<string> ioSystems = new(StringComparer.Ordinal);

        private readonly List<(string A, string B)> links = [];

        public void Children(XElement parent, Context context)
        {
            foreach (var child in parent.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "InternalElement":
                        Element(child, context);
                        break;
                    case "InternalLink":
                        if (LinkEnd(child.AttributeValue("RefPartnerSideA")) is { } a && LinkEnd(child.AttributeValue("RefPartnerSideB")) is { } b)
                        {
                            links.Add((a, b));
                        }
                        break;
                }
            }
        }

        /// <summary>RefPartnerSideA/B are "&lt;element id&gt;:&lt;interface name&gt;".</summary>
        private static string? LinkEnd(string? side)
        {
            var colon = side?.IndexOf(':') ?? -1;
            return colon > 0 ? side![..colon] : null;
        }

        private void Element(XElement element, Context context)
        {
            var id = element.AttributeValue("ID") ?? "";
            var name = CleanName(element.AttributeValue("Name"));
            var roles = Roles(element);
            foreach (var unknown in roles.Where(r => !Known.Contains(r)))
            {
                UnknownRoles.Add(unknown);
            }
            var role = Known.FirstOrDefault(roles.Contains);
            if (role is not null && Skipped.Contains(role))
            {
                return;
            }
            if (context.Device is not null && id.Length > 0)
            {
                owner[id] = context.Device;
            }

            switch (role)
            {
                case RoleProject:
                    ProjectName ??= name;
                    Children(element, context);
                    break;
                case RoleFolder:
                    Children(element, context with { Folder = context.Folder is null ? name : $"{context.Folder}/{name}" });
                    break;
                case RoleDevice:
                    var device = new DeviceBuilder(id, name, Attr(element, "TypeIdentifier"), context.Folder);
                    Devices.Add(device);
                    owner[id] = device;
                    Children(element, new Context(device, null, null, context.Folder, ""));
                    break;
                case RoleDeviceItem or RoleInterface or RolePort when context.Device is not null:
                    Children(element, Item(element, id, name, context));
                    break;
                case RoleIoSystem when context.Device is not null:
                    context.Device.IoSystems.Add(new AmlIoSystem(id, name, context.CpuItemId, context.ItemId));
                    ioSystems.Add(id);
                    break;
                default:
                    // Unknown role, no role, or a device item outside a device: walk on, nothing is lost below.
                    Children(element, context);
                    break;
            }
        }

        private Context Item(XElement element, string id, string name, Context context)
        {
            if (string.Equals(Attr(element, "BuiltIn"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return context;
            }
            var number = Attr(element, "PositionNumber") ?? name;
            var position = context.Position.Length == 0 ? number : $"{context.Position}/{number}";
            var deviceItemType = Attr(element, "DeviceItemType");
            context.Device!.Items.Add(new AmlItem(
                id,
                name,
                position,
                Attr(element, "TypeIdentifier"),
                Attr(element, "TypeName"),
                Attr(element, "FirmwareVersion"),
                deviceItemType));
            return context with
            {
                ItemId = id,
                CpuItemId = deviceItemType == "CPU" ? id : context.CpuItemId,
                Position = position,
            };
        }

        /// <summary>"Link To IoSystem": one end is an IO system, the other an interface inside a device.</summary>
        public void ResolveLinks()
        {
            foreach (var (a, b) in links)
            {
                var (ioSystem, other) = ioSystems.Contains(a) ? (a, b) : ioSystems.Contains(b) ? (b, a) : (null, null);
                if (ioSystem is not null && owner.TryGetValue(other!, out var device))
                {
                    device.LinkedIoSystemIds.Add(ioSystem);
                }
            }
        }
    }
}
