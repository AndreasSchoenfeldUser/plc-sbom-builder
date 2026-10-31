using System.Security.Cryptography;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;

namespace PlcSbom.Core.Adapters.Studio5000;

/// <summary>What the caller (later machine.yml, section 7.4) tells the adapter about a station.</summary>
public sealed record Studio5000StationOptions
{
    /// <summary>Station name in the SBOM.</summary>
    public required string StationName { get; init; }

    /// <summary>Source file relative to the repository, with '/', as written into the SBOM.</summary>
    public required string SourcePath { get; init; }
}

/// <summary>
/// Adapter studio5000 (CLAUDE.md section 5.2): full L5X export of a Logix controller from Studio 5000.
/// Field table and rules in docs/adapter-studio5000.md.
/// </summary>
public static class Studio5000Adapter
{
    /// <summary>Name of the engineering tool in metadata.tools; the L5X names only its SoftwareRevision.</summary>
    public const string ToolName = "Studio 5000 Logix Designer";

    /// <summary>CIP vendor id 1, the only vendor id in the real fixtures (docs/entscheidungen.md E-017).</summary>
    public const string RockwellVendorId = "1";

    public const string RockwellName = "Rockwell Automation";

    public const string EKeyDisabled = "Disabled";

    public static Station Read(string fullPath, Studio5000StationOptions options)
    {
        var content = ReadFile(fullPath);
        var project = L5xReader.Read(content, options.SourcePath);
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

    public static Station ToStation(L5xProject project, string sourceSha256, Studio5000StationOptions options)
    {
        var controller = project.Controller;
        if (project.TargetType != "Controller" || controller is null || controller.Use != "Target")
        {
            throw new UsageException(
                "STUDIO5000_NOT_CONTROLLER",
                $"{options.SourcePath} ist ein Export von „{project.TargetName}“ (TargetType „{project.TargetType}“), nicht des ganzen Controllers. In Studio 5000 das Projekt mit „Speichern unter … L5X“ exportieren.");
        }

        var devices = new List<Device>();
        foreach (var (module, isController) in TreeOrder(project.Modules))
        {
            devices.Add(ToDevice(module, isController, controller));
        }

        var software = project.AddOnInstructions
            .Select(ToSoftware)
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Version, StringComparer.Ordinal)
            .ToList();

        return new Station
        {
            Name = options.StationName,
            AdapterId = AdapterIds.Studio5000,
            SourcePath = options.SourcePath,
            SourceSha256 = sourceSha256,
            // ExportDate has no time zone ("Fri Nov 15 01:11:39 2024"); converting it would need an invented offset.
            EngineeringTool = project.SoftwareRevision is { } revision ? new ToolInfo(ToolName, revision) : null,
            Devices = devices,
            Software = software,
        };
    }

    /// <summary>
    /// Controller first (the module that is its own parent), then its modules depth-first in file order.
    /// Modules whose parent chain does not reach the controller follow in file order.
    /// </summary>
    private static List<(L5xModule Module, bool IsController)> TreeOrder(IReadOnlyList<L5xModule> modules)
    {
        var result = new List<(L5xModule, bool)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var root = modules.FirstOrDefault(m => m.ParentModule == m.Name);

        void Visit(L5xModule module, bool isController)
        {
            if (!visited.Add(module.Name))
            {
                return;
            }
            result.Add((module, isController));
            foreach (var child in modules.Where(m => m.ParentModule == module.Name && m.Name != module.Name))
            {
                Visit(child, isController: false);
            }
        }

        if (root is not null)
        {
            Visit(root, isController: true);
        }
        foreach (var module in modules)
        {
            Visit(module, isController: false);
        }
        return result;
    }

    private static Device ToDevice(L5xModule module, bool isController, L5xController controller)
    {
        var keys = new SortedDictionary<string, string>(StringComparer.Ordinal);
        AddKey(keys, "VendorId", module.Vendor);
        AddKey(keys, "ProductType", module.ProductType);
        AddKey(keys, "ProductCode", module.ProductCode);

        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (module.Ekey is not null)
        {
            properties["device:ekey"] = module.Ekey;
        }

        // The controller's firmware is the project revision (Controller@MajorRev/MinorRev), modules
        // carry their configured revision in Major/Minor.
        var firmware = isController
            ? Revision(controller.MajorRev, controller.MinorRev) ?? Revision(module.Major, module.Minor)
            : Revision(module.Major, module.Minor);

        var gaps = new List<Gap>();
        // On the controller module itself keying is Disabled in every real export; the major revision is
        // bound by the project anyway (docs/entscheidungen.md E-017). Only other modules get the gap.
        if (!isController && module.Ekey == EKeyDisabled)
        {
            gaps.Add(new Gap(GapCatalog.EkeyDisabled,
                $"Die elektronische Kodierung von {module.Name} ist aus. Das Modul im Feld kann eine andere Firmware als {firmware ?? "die projektierte"} haben, auch eine andere Hauptversion."));
        }
        if (module.CatalogNumber is null)
        {
            gaps.Add(new Gap(GapCatalog.OrderNumberNotInProject, $"Das Modul {module.Name} hat im Export keine CatalogNumber."));
        }

        var isRockwell = module.Vendor == RockwellVendorId;
        if (!isRockwell)
        {
            gaps.Add(new Gap(GapCatalog.SupplierUnknown, module.Vendor is null
                ? $"Das Modul {module.Name} hat im Export keine Herstellerkennung."
                : $"Das Modul {module.Name} hat die Herstellerkennung {module.Vendor}. Der Export nennt keinen Herstellernamen dazu."));
        }

        return new Device
        {
            // The L5X has no product name; the catalogue number is the type the project shows.
            TypeName = module.CatalogNumber ?? module.Name,
            Vendor = isRockwell ? new Party(RockwellName) : null,
            VendorOrigin = isRockwell ? ValueOrigin.Assumed : ValueOrigin.Source,
            OrderNumber = module.CatalogNumber,
            DeviceName = module.Name,
            Position = Position(module),
            IsController = isController,
            Firmware = firmware,
            FirmwareOrigin = firmware is null ? FirmwareOrigin.NotInProject : FirmwareOrigin.Projected,
            VendorKeys = keys,
            SourceProperties = properties,
            Gaps = gaps,
        };
    }

    /// <summary>Parent and slot on a backplane, else parent and module name. Never an IP address.</summary>
    private static string? Position(L5xModule module) =>
        module.ParentModule is null ? null
        : module.Slot is { } slot ? $"{module.ParentModule}/{slot}"
        : $"{module.ParentModule}/{module.Name}";

    private static string? Revision(string? major, string? minor) =>
        major is null ? null : minor is null ? major : $"{major}.{minor}";

    private static SoftwareItem ToSoftware(L5xAddOnInstruction aoi)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (aoi.RevisionExtension is not null)
        {
            properties["aoi:revisionExtension"] = aoi.RevisionExtension;
        }
        return new SoftwareItem
        {
            Kind = SoftwareKind.AddOnInstruction,
            Name = aoi.Name,
            Vendor = aoi.Vendor is { } vendor ? new Party(vendor) : null,
            Version = aoi.Revision,
            SourceProperties = properties,
        };
    }

    private static void AddKey(SortedDictionary<string, string> keys, string name, string? value)
    {
        if (value is not null)
        {
            keys[name] = value;
        }
    }
}
