using System.Xml.Linq;

namespace PlcSbom.Core.Adapters.TwinCat;

// Raw inventory of a TwinCAT 3 project (CLAUDE.md section 5.3, docs/adapter-twincat.md): the system
// manager file (.tsproj) with its I/O tree and the PLC projects (.plcproj) it references.
// Values stay as written; interpretation happens in TwinCatAdapter. Network data (AmsNetId,
// TargetNetId, IP addresses of PROFINET boxes, EtherCAT addresses) and the BIC (serial numbers of
// scanned devices) are never read.

/// <summary>Whole project: XAE version, I/O devices and PLC projects.</summary>
public sealed record TwinCatProject(
    string? TcVersion,
    IReadOnlyList<TwinCatIoDevice> IoDevices,
    IReadOnlyList<TwinCatPlcProject> PlcProjects);

/// <summary>An I/O device (EtherCAT master, PROFINET controller …) with its boxes.</summary>
public sealed record TwinCatIoDevice(
    string Name,
    string? DevType,
    bool Disabled,
    string? File,
    IReadOnlyList<TwinCatBox> Boxes);

/// <summary>
/// A box. <see cref="Fieldbus"/> is the name of the protocol element under the box (EtherCAT, Profinet);
/// the identity is read only for EtherCAT.
/// </summary>
public sealed record TwinCatBox(
    string Id,
    string Name,
    string? BoxType,
    string? Fieldbus,
    string? VendorId,
    string? ProductCode,
    string? RevisionNo,
    string? Desc,
    string? Type,
    IReadOnlyList<TwinCatBox> Children);

/// <summary>A PLC project referenced by the .tsproj, with its library references.</summary>
public sealed record TwinCatPlcProject(
    string Name,
    string File,
    string? ProgramVersion,
    IReadOnlyList<TwinCatLibrary> Libraries);

public enum TwinCatLibraryKind
{
    /// <summary>PlaceholderReference: a placeholder with DefaultResolution.</summary>
    Placeholder,

    /// <summary>PlaceholderResolution for a placeholder the project does not reference itself.</summary>
    ResolutionOnly,

    /// <summary>LibraryReference with title, version and company in Include.</summary>
    Direct,
}

/// <summary>One library entry of a .plcproj.</summary>
public sealed record TwinCatLibrary(
    TwinCatLibraryKind Kind,
    string Name,
    string? DefaultResolution,
    string? Resolution,
    string? Namespace,
    bool SystemLibrary);

/// <summary>Reads a .tsproj and the .plcproj files it references.</summary>
public static class TwinCatReader
{
    public const string RootElement = "TcSmProject";

    /// <summary>Folder TwinCAT stores independent PLC project files in, relative to the .tsproj (fixture SPT EtherCAT Diagnostics).</summary>
    public const string PlcConfigFolder = "_Config/PLC";

    public static TwinCatProject Read(string tsprojPath, byte[] content, string displayPath)
    {
        var root = SafeXml.Load(content, displayPath).Root!;
        if (root.Name.LocalName != RootElement)
        {
            throw new UsageException(
                "NOT_TSPROJ",
                $"Die Datei {displayPath} ist kein TwinCAT-3-Projekt (Wurzelelement „{root.Name.LocalName}“ statt „{RootElement}“). Angegeben wird die .tsproj-Datei des Projekts.");
        }

        var project = root.ElementNamed("Project");
        var devices = project?.ElementNamed("Io")?.ElementsNamed("Device").Select(ReadDevice).ToList() ?? [];

        var baseDir = Path.GetDirectoryName(Path.GetFullPath(tsprojPath))!;
        var plcProjects = new List<TwinCatPlcProject>();
        foreach (var plc in project?.ElementNamed("Plc")?.ElementsNamed("Project") ?? [])
        {
            plcProjects.Add(ReadPlcReference(plc, baseDir, displayPath));
        }

        return new TwinCatProject(root.AttributeValue("TcVersion"), devices, plcProjects);
    }

    private static TwinCatIoDevice ReadDevice(XElement device) => new(
        device.ChildText("Name") ?? "",
        device.AttributeValue("DevType"),
        IsTrue(device.AttributeValue("Disabled")),
        device.AttributeValue("File"),
        device.ElementsNamed("Box").Select(ReadBox).ToList());

    private static TwinCatBox ReadBox(XElement box)
    {
        // The protocol element is the child named after the fieldbus (EtherCAT, Profinet); Name, ImageId,
        // Vars, Box and Comment are structure.
        var protocol = box.Elements().FirstOrDefault(e => e.Name.LocalName is "EtherCAT" or "Profinet");
        var ethercat = protocol?.Name.LocalName == "EtherCAT" ? protocol : null;
        return new TwinCatBox(
            box.AttributeValue("Id") ?? "",
            box.ChildText("Name") ?? "",
            box.AttributeValue("BoxType"),
            protocol?.Name.LocalName,
            ethercat?.AttributeValue("VendorId"),
            ethercat?.AttributeValue("ProductCode"),
            ethercat?.AttributeValue("RevisionNo"),
            ethercat?.AttributeValue("Desc"),
            ethercat?.AttributeValue("Type"),
            box.ElementsNamed("Box").Select(ReadBox).ToList());
    }

    /// <summary>
    /// Plc/Project either names the .plcproj directly (PrjFilePath, relative to the .tsproj) or points to an
    /// independent project file (File, in _Config/PLC/) whose Project@PrjFilePath is relative to itself.
    /// </summary>
    private static TwinCatPlcProject ReadPlcReference(XElement plc, string baseDir, string displayPath)
    {
        var name = plc.AttributeValue("Name");
        var prjFilePath = plc.AttributeValue("PrjFilePath");
        var prjBase = baseDir;
        if (prjFilePath is null && plc.AttributeValue("File") is { } file)
        {
            var xtiPath = Path.Combine(baseDir, PlcConfigFolder, file);
            if (!File.Exists(xtiPath))
            {
                throw new UsageException(
                    "TWINCAT_FILE_NOT_FOUND",
                    $"{displayPath} verweist auf das SPS-Projekt „{file}“, die Datei {PlcConfigFolder}/{file} gibt es nicht.");
            }
            var xti = SafeXml.Load(File.ReadAllBytes(xtiPath), file).Root!.ElementNamed("Project");
            name ??= xti?.AttributeValue("Name");
            prjFilePath = xti?.AttributeValue("PrjFilePath");
            prjBase = Path.GetDirectoryName(xtiPath)!;
        }
        if (prjFilePath is null)
        {
            throw new UsageException("TWINCAT_PLCPROJ_MISSING", $"{displayPath} enthält ein SPS-Projekt „{name}“ ohne Pfad zur .plcproj-Datei.");
        }

        // Paths in TwinCAT files use '\'; normalised for Linux.
        var full = Path.GetFullPath(Path.Combine(prjBase, prjFilePath.Replace('\\', '/')));
        var relative = Path.GetRelativePath(baseDir, full).Replace('\\', '/');
        if (!File.Exists(full))
        {
            throw new UsageException("TWINCAT_FILE_NOT_FOUND", $"{displayPath} verweist auf das SPS-Projekt {relative}, die Datei gibt es nicht.");
        }
        var plcproj = SafeXml.Load(File.ReadAllBytes(full), relative).Root!;
        return new TwinCatPlcProject(
            name ?? plcproj.ElementNamed("PropertyGroup")?.ChildText("Name") ?? relative,
            relative,
            plcproj.ElementsNamed("PropertyGroup").Select(g => g.ChildText("ProgramVersion")).FirstOrDefault(v => v is not null),
            ReadLibraries(plcproj));
    }

    private static List<TwinCatLibrary> ReadLibraries(XElement plcproj)
    {
        var items = plcproj.ElementsNamed("ItemGroup").SelectMany(g => g.Elements()).ToList();
        var resolutions = items.Where(e => e.Name.LocalName == "PlaceholderResolution")
            .Select(e => (Placeholder: e.AttributeValue("Include"), Resolution: e.ChildText("Resolution")))
            .Where(r => r.Placeholder is not null && r.Resolution is not null)
            .GroupBy(r => r.Placeholder!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Resolution!, StringComparer.OrdinalIgnoreCase);

        var result = new List<TwinCatLibrary>();
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var include = item.AttributeValue("Include");
            if (include is null)
            {
                continue;
            }
            switch (item.Name.LocalName)
            {
                case "PlaceholderReference":
                    referenced.Add(include);
                    result.Add(new TwinCatLibrary(
                        TwinCatLibraryKind.Placeholder,
                        include,
                        item.ChildText("DefaultResolution"),
                        resolutions.GetValueOrDefault(include),
                        item.ChildText("Namespace"),
                        IsTrue(item.ChildText("SystemLibrary"))));
                    break;
                case "LibraryReference":
                    result.Add(new TwinCatLibrary(
                        TwinCatLibraryKind.Direct,
                        include,
                        null,
                        null,
                        item.ChildText("Namespace"),
                        IsTrue(item.ChildText("SystemLibrary"))));
                    break;
            }
        }
        // Resolutions of placeholders the project does not reference itself apply to libraries its
        // libraries reference (e.g. Tc2_Utilities in TcoCore.plcproj).
        foreach (var (placeholder, resolution) in resolutions.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            if (!referenced.Contains(placeholder))
            {
                result.Add(new TwinCatLibrary(TwinCatLibraryKind.ResolutionOnly, placeholder, null, resolution, null, false));
            }
        }
        return result;
    }

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
