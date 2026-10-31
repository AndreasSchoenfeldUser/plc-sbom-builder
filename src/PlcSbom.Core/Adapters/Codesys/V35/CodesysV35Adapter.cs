using System.Security.Cryptography;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;

namespace PlcSbom.Core.Adapters.Codesys.V35;

/// <summary>What the caller (later machine.yml, section 7.4) tells the adapter about a station.</summary>
public sealed record CodesysStationOptions
{
    /// <summary>Station name in the SBOM.</summary>
    public required string StationName { get; init; }

    /// <summary>Source file relative to the repository, with '/', as written into the SBOM.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Device name of the controller in the project; required if the export holds several.</summary>
    public string? Plc { get; init; }
}

/// <summary>
/// Adapter codesys, source v35 (CLAUDE.md section 5.1.1): PLCopen XML export of CODESYS V3.5.
/// Field table and rules in docs/adapter-codesys.md.
/// </summary>
public static class CodesysV35Adapter
{
    public const string Source = "v35";

    /// <summary>Company CODESYS writes for libraries of its installation; not an organisation (docs/adapter-codesys.md).</summary>
    public const string SystemCompany = "System";

    public static Station Read(string fullPath, CodesysStationOptions options)
    {
        var content = ReadFile(fullPath);
        var project = PlcOpenReader.Read(content, options.SourcePath);
        return ToStation(project, Convert.ToHexStringLower(SHA256.HashData(content)), options);
    }

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

    public static Station ToStation(PlcOpenProject project, string sourceSha256, CodesysStationOptions options)
    {
        var controller = SelectController(project, options);
        var devices = new List<Device>();
        if (controller is not null)
        {
            AddDevices(devices, controller, position: null, isController: true);
        }

        // Libraries of the POU pool apply to every device; those of the applications only to their device.
        var libraries = project.ProjectLibraries
            .Concat(controller?.Applications.SelectMany(a => a.Libraries) ?? [])
            .Select(ToSoftware)
            .GroupBy(s => (s.Name, s.Version, s.VersionResolved, Company: s.SourceProperties.GetValueOrDefault("library:company")))
            .Select(g => g.First())
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Version, StringComparer.Ordinal)
            .ToList();

        return new Station
        {
            Name = options.StationName,
            AdapterId = AdapterIds.Codesys,
            AdapterSource = Source,
            SourcePath = options.SourcePath,
            SourceSha256 = sourceSha256,
            // creationDateTime has no time zone in the export; converting it would need an invented offset.
            EngineeringTool = Tool(project),
            Devices = devices,
            Software = libraries,
        };
    }

    /// <summary>Controllers are the top-level devices; <see cref="CodesysStationOptions.Plc"/> picks one by name.</summary>
    private static PlcOpenDevice? SelectController(PlcOpenProject project, CodesysStationOptions options)
    {
        var names = string.Join(", ", project.Devices.Select(d => d.Name));
        if (options.Plc is not null)
        {
            return project.Devices.FirstOrDefault(d => d.Name == options.Plc)
                ?? throw new UsageException(
                    "CODESYS_PLC_NOT_FOUND",
                    project.Devices.Count == 0
                        ? $"Die Steuerung „{options.Plc}“ steht nicht in {options.SourcePath}. Der Export enthält kein Gerät."
                        : $"Die Steuerung „{options.Plc}“ steht nicht in {options.SourcePath}. Gefunden: {names}.");
        }
        return project.Devices.Count switch
        {
            0 => null,
            1 => project.Devices[0],
            _ => throw new UsageException(
                "CODESYS_PLC_REQUIRED",
                $"{options.SourcePath} enthält {project.Devices.Count} Steuerungen ({names}). Für die Station {options.StationName} mit „plc“ eine davon wählen."),
        };
    }

    private static void AddDevices(List<Device> target, PlcOpenDevice device, string? position, bool isController)
    {
        target.Add(ToDevice(device, position, isController));
        foreach (var child in device.Children)
        {
            AddDevices(target, child, position is null ? child.Name : $"{position}/{child.Name}", isController: false);
        }
    }

    private static Device ToDevice(PlcOpenDevice device, string? position, bool isController)
    {
        var keys = new SortedDictionary<string, string>(StringComparer.Ordinal);
        AddKey(keys, "DeviceType", device.Type);
        AddKey(keys, "DeviceId", device.Id);
        AddKey(keys, "DeviceDescriptionVersion", device.Version);

        var gaps = new List<Gap>
        {
            new(GapCatalog.FirmwareNotInProject, device.Version is null
                ? "Der PLCopen-Export führt keine Firmware-Version für dieses Gerät."
                : $"Der PLCopen-Export führt keine Firmware-Version. Die Version {device.Version} in DeviceIdentification gehört zur Gerätebeschreibung, nicht zur Firmware."),
            new(GapCatalog.OrderNumberNotInProject, "Der PLCopen-Export nennt für Geräte keine Bestellnummer."),
            new(GapCatalog.SupplierUnknown, "Der PLCopen-Export nennt für Geräte keinen Hersteller."),
        };

        return new Device
        {
            // The export has no product name, only the identification of the device description.
            TypeName = $"Gerät {device.Type ?? "?"}/{device.Id ?? "?"}",
            DeviceName = device.Name,
            Position = position,
            IsController = isController,
            FirmwareOrigin = FirmwareOrigin.Unknown,
            VendorKeys = keys,
            Gaps = gaps,
        };
    }

    private static SoftwareItem ToSoftware(PlcOpenLibrary library)
    {
        var parsed = library.Parsed;
        var gaps = new List<Gap>();
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["library:reference"] = library.Reference,
            ["library:resolution"] = ResolutionText(library.Resolution),
        };

        if (parsed is null)
        {
            gaps.Add(new Gap(GapCatalog.VersionUnresolved, library.Effective is null
                ? $"Der Platzhalter „{library.Reference}“ hat im Export weder Umleitung noch Standardauflösung."
                : $"Die Bibliotheksangabe „{library.Effective}“ folgt nicht dem Muster „Titel, Version (Hersteller)“."));
            return new SoftwareItem
            {
                Kind = SoftwareKind.Library,
                Name = library.Placeholder ?? library.Effective ?? library.Reference,
                VersionResolved = false,
                SourceProperties = properties,
                Gaps = gaps,
            };
        }

        properties["library:company"] = parsed.Company;
        var resolved = !parsed.IsNewest && library.Resolution != LibraryResolution.Default;
        if (parsed.IsNewest)
        {
            gaps.Add(new Gap(GapCatalog.VersionUnresolved,
                $"„{parsed}“ verlangt die neueste installierte Version. Welche das beim Übersetzen war, steht nicht im Export."));
        }
        else if (library.Resolution == LibraryResolution.Default)
        {
            gaps.Add(new Gap(GapCatalog.VersionUnresolved,
                $"Der Platzhalter „{library.Reference}“ ist im Projekt nicht umgeleitet. Die Standardauflösung nennt {parsed.Version}, die Gerätebeschreibung kann eine andere Version festlegen."));
        }

        Party? vendor = null;
        if (parsed.Company == SystemCompany)
        {
            gaps.Add(new Gap(GapCatalog.SupplierUnknown,
                "Als Hersteller steht nur „System“ (Bibliothek aus der CODESYS-Installation). Eine Organisation lässt sich daraus nicht ableiten."));
        }
        else
        {
            vendor = new Party(parsed.Company);
        }

        return new SoftwareItem
        {
            Kind = SoftwareKind.Library,
            Name = parsed.Title,
            Vendor = vendor,
            Version = parsed.Version,
            VersionResolved = resolved,
            SourceProperties = properties,
            Gaps = gaps,
        };
    }

    private static ToolInfo? Tool(PlcOpenProject project)
    {
        if (project.ProductName is null || project.ProductVersion is null)
        {
            return null;
        }
        // productVersion repeats the product name ("CODESYS V3.5 SP15"); only the prefix is cut off.
        var prefix = project.ProductName + " ";
        var version = project.ProductVersion.StartsWith(prefix, StringComparison.Ordinal)
            ? project.ProductVersion[prefix.Length..]
            : project.ProductVersion;
        return new ToolInfo(project.ProductName, version, Vendor: project.CompanyName);
    }

    private static string ResolutionText(LibraryResolution resolution) => resolution switch
    {
        LibraryResolution.Fixed => "fixed",
        LibraryResolution.Redirection => "redirection",
        LibraryResolution.Default => "default",
        LibraryResolution.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null),
    };

    private static void AddKey(SortedDictionary<string, string> keys, string name, string? value)
    {
        if (value is not null)
        {
            keys[name] = value;
        }
    }
}
