using System.Security.Cryptography;
using PlcSbom.Core.Adapters.Codesys;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;

namespace PlcSbom.Core.Adapters.TwinCat;

/// <summary>What the caller (later machine.yml, section 7.4) tells the adapter about a station.</summary>
public sealed record TwinCatStationOptions
{
    /// <summary>Station name in the SBOM.</summary>
    public required string StationName { get; init; }

    /// <summary>Source file relative to the repository, with '/', as written into the SBOM.</summary>
    public required string SourcePath { get; init; }
}

/// <summary>
/// Adapter twincat (CLAUDE.md section 5.3): TwinCAT 3 system manager project (.tsproj) with its EtherCAT
/// I/O tree and the PLC projects (.plcproj) it references. Field table and rules in docs/adapter-twincat.md.
/// </summary>
public static class TwinCatAdapter
{
    public const string ToolName = "TwinCAT XAE";

    public const string RuntimeName = "TwinCAT 3 Runtime";

    /// <summary>EtherCAT vendor id of Beckhoff, the only vendor of all terminals in the fixtures (E-018).</summary>
    public const string BeckhoffVendorId = "0x00000002";

    public const string BeckhoffName = "Beckhoff Automation GmbH & Co. KG";

    public const string EtherCat = "EtherCAT";

    public static Station Read(string fullPath, TwinCatStationOptions options)
    {
        if (!File.Exists(fullPath))
        {
            throw new UsageException("SOURCE_NOT_FOUND", $"Die Quelldatei {fullPath} gibt es nicht.");
        }
        var content = File.ReadAllBytes(fullPath);
        var project = TwinCatReader.Read(fullPath, content, options.SourcePath);
        return ToStation(project, Convert.ToHexStringLower(SHA256.HashData(content)), options);
    }

    /// <summary>"#x00120000" → "0x00120000"; other values unchanged.</summary>
    public static string? Hex(string? value) =>
        value is not null && value.StartsWith("#x", StringComparison.OrdinalIgnoreCase) ? "0x" + value[2..].ToLowerInvariant() : value;

    public static Station ToStation(TwinCatProject project, string sourceSha256, TwinCatStationOptions options)
    {
        var devices = new List<Device>();
        var notRead = new List<string>();
        foreach (var io in project.IoDevices)
        {
            if (io.File is not null)
            {
                notRead.Add($"{io.Name} (Konfiguration in eigener Datei {io.File})");
            }
            foreach (var box in io.Boxes)
            {
                AddBoxes(devices, notRead, io, box, io.Name);
            }
        }

        var software = new List<SoftwareItem> { Runtime() };
        software.AddRange(project.PlcProjects
            .SelectMany(p => p.Libraries)
            .Select(ToSoftware)
            // "newest" (LibraryReference) and "*" (placeholder) both mean the newest installed version.
            .GroupBy(s => (s.Name, Version: s.Version == "newest" ? "*" : s.Version, s.VersionResolved, Company: s.SourceProperties.GetValueOrDefault("library:company")))
            .Select(g => g.First())
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Version, StringComparer.Ordinal));

        var gaps = new List<Gap>();
        if (notRead.Count > 0)
        {
            gaps.Add(new Gap(GapCatalog.DevicesNotRead,
                $"Das Projekt enthält Geräte, die der Adapter twincat nicht ausliest (nur EtherCAT-Teilnehmer im Projekt): {string.Join(", ", notRead)}."));
        }

        return new Station
        {
            Name = options.StationName,
            AdapterId = AdapterIds.TwinCat,
            SourcePath = options.SourcePath,
            SourceSha256 = sourceSha256,
            // TcVersion is the XAE version that saved the project, not the runtime on the target.
            EngineeringTool = project.TcVersion is { } version ? new ToolInfo(ToolName, version) : null,
            Devices = devices,
            Software = software,
            Gaps = gaps,
        };
    }

    private static void AddBoxes(List<Device> devices, List<string> notRead, TwinCatIoDevice io, TwinCatBox box, string parentPosition)
    {
        var position = $"{parentPosition}/{box.Id}";
        if (box.Fieldbus == EtherCat && box.VendorId is not null)
        {
            devices.Add(ToDevice(io, box, position));
        }
        else
        {
            notRead.Add($"{box.Name} ({box.Fieldbus ?? "Box-Typ " + box.BoxType} an {io.Name})");
        }
        foreach (var child in box.Children)
        {
            AddBoxes(devices, notRead, io, child, position);
        }
    }

    private static Device ToDevice(TwinCatIoDevice io, TwinCatBox box, string position)
    {
        var vendorId = Hex(box.VendorId);
        var keys = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["VendorId"] = vendorId! };
        AddKey(keys, "ProductCode", Hex(box.ProductCode));
        AddKey(keys, "RevisionNo", Hex(box.RevisionNo));

        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (io.Disabled)
        {
            properties["device:disabled"] = "true";
        }

        var typeName = box.Desc ?? box.Type ?? box.Name;
        var gaps = new List<Gap>
        {
            new(GapCatalog.FirmwareNotInProject, box.RevisionNo is null
                ? "Das TwinCAT-Projekt führt für EtherCAT-Teilnehmer keine Firmware-Version. Den Ist-Stand liefert das Gerät selbst (CoE-Objekt 0x100A)."
                : $"Das TwinCAT-Projekt führt für EtherCAT-Teilnehmer keine Firmware-Version. RevisionNo {Hex(box.RevisionNo)} ist die Revision aus der Gerätebeschreibung (ESI), nicht die Firmware. Den Ist-Stand liefert das Gerät selbst (CoE-Objekt 0x100A)."),
            new(GapCatalog.OrderNumberNotInProject,
                $"Das TwinCAT-Projekt nennt keine Bestellnummer. „{typeName}“ ist die Kurzbezeichnung aus der Gerätebeschreibung; das Gerät ist über VendorId, ProductCode und RevisionNo bestimmt."),
        };

        var isBeckhoff = vendorId == BeckhoffVendorId;
        if (!isBeckhoff)
        {
            gaps.Add(new Gap(GapCatalog.SupplierUnknown, $"{box.Name} hat die EtherCAT-Herstellerkennung {vendorId}. Das Projekt nennt keinen Herstellernamen dazu."));
        }

        return new Device
        {
            TypeName = typeName,
            Vendor = isBeckhoff ? new Party(BeckhoffName) : null,
            VendorOrigin = isBeckhoff ? ValueOrigin.Assumed : ValueOrigin.Source,
            DeviceName = box.Name,
            Position = position,
            FirmwareOrigin = FirmwareOrigin.NotInProject,
            VendorKeys = keys,
            SourceProperties = properties,
            Gaps = gaps,
        };
    }

    /// <summary>
    /// Every TwinCAT 3 project runs on a TwinCAT 3 runtime (XAR). Its version is not in the project
    /// (runtime-version-unknown, added by the gap rules); name and vendor are assumptions (E-018).
    /// </summary>
    private static SoftwareItem Runtime() => new()
    {
        Kind = SoftwareKind.Runtime,
        Name = RuntimeName,
        Vendor = new Party(BeckhoffName),
        VendorOrigin = ValueOrigin.Assumed,
        Origin = ValueOrigin.Assumed,
        Gaps =
        [
            new Gap(GapCatalog.RuntimeVersionUnknown,
                "Die Version der TwinCAT-Laufzeit auf dem Zielsystem steht nicht im Projekt. TcVersion im .tsproj ist die Version der Engineering-Umgebung (XAE)."),
        ],
    };

    private static SoftwareItem ToSoftware(TwinCatLibrary library)
    {
        var (resolution, effective) = library.Kind switch
        {
            TwinCatLibraryKind.Direct => ("fixed", ParseDirect(library.Name)),
            TwinCatLibraryKind.ResolutionOnly => ("indirect", LibraryReference.Parse(library.Resolution)),
            _ when library.Resolution is not null => ("redirection", LibraryReference.Parse(library.Resolution)),
            _ when library.DefaultResolution is not null => ("default", LibraryReference.Parse(library.DefaultResolution)),
            _ => ("none", null),
        };

        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["library:reference"] = library.Name,
            ["library:resolution"] = resolution,
        };
        var gaps = new List<Gap>();
        if (effective is null)
        {
            var text = library.Kind == TwinCatLibraryKind.Direct ? library.Name : library.Resolution ?? library.DefaultResolution;
            gaps.Add(new Gap(GapCatalog.VersionUnresolved, text is null
                ? $"Der Platzhalter „{library.Name}“ hat im Projekt weder Auflösung noch Standardauflösung."
                : $"Die Bibliotheksangabe „{text}“ folgt nicht dem erwarteten Muster."));
            return new SoftwareItem
            {
                Kind = SoftwareKind.Library,
                Name = library.Name,
                VersionResolved = false,
                SourceProperties = properties,
                Gaps = gaps,
            };
        }

        properties["library:company"] = effective.Company;
        var newest = effective.Version is "*" or "newest";
        if (newest)
        {
            gaps.Add(new Gap(GapCatalog.VersionUnresolved,
                $"„{effective.Title}“ verlangt die neueste installierte Version ({effective.Version}). Welche das beim Übersetzen war, steht nicht im Projekt."));
        }
        else if (resolution == "default")
        {
            gaps.Add(new Gap(GapCatalog.VersionUnresolved,
                $"Der Platzhalter „{library.Name}“ ist im Projekt nicht aufgelöst. Die Standardauflösung nennt {effective.Version}; welche Version beim Übersetzen galt, steht nicht im Projekt."));
        }

        Party? vendor = null;
        if (string.Equals(effective.Company, CodesysSystemCompany, StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add(new Gap(GapCatalog.SupplierUnknown,
                $"Als Hersteller steht nur „{effective.Company}“ (Bibliothek der Installation). Eine Organisation lässt sich daraus nicht ableiten."));
        }
        else
        {
            vendor = new Party(effective.Company);
        }

        return new SoftwareItem
        {
            Kind = SoftwareKind.Library,
            Name = effective.Title,
            Vendor = vendor,
            Version = effective.Version,
            VersionResolved = !newest && resolution != "default",
            SourceProperties = properties,
            Gaps = gaps,
        };
    }

    private const string CodesysSystemCompany = "System";

    /// <summary>LibraryReference@Include is "Title,Version,Company" (TcoCore.plcproj: "Tc2_EtherCAT,newest,Beckhoff Automation GmbH").</summary>
    private static LibraryReference? ParseDirect(string include)
    {
        var parts = include.Split(',', 3);
        return parts.Length == 3 && parts.All(p => p.Trim().Length > 0)
            ? new LibraryReference(parts[0].Trim(), parts[1].Trim(), parts[2].Trim())
            : null;
    }

    private static void AddKey(SortedDictionary<string, string> keys, string name, string? value)
    {
        if (value is not null)
        {
            keys[name] = value;
        }
    }
}
