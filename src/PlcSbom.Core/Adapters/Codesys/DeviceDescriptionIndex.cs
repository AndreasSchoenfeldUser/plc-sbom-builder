using System.Security.Cryptography;

namespace PlcSbom.Core.Adapters.Codesys;

/// <summary>
/// One Device element of a CODESYS device description (*.devdesc.xml, schema DeviceDescription-1.0).
/// Belegt in fixtures/codesys/devdesc/ (stefandreyer/I2C-Multiplex).
/// </summary>
public sealed record DeviceDescription(
    string File,
    string Sha256,
    string? Type,
    string? Id,
    string? Version,
    string? Name,
    string? Vendor,
    string? OrderNumber);

/// <summary>Result of looking up a DeviceIdentification.</summary>
public sealed record DeviceDescriptionMatch(DeviceDescription? Found, IReadOnlyList<DeviceDescription> Candidates)
{
    public bool IsAmbiguous => Found is null && Candidates.Count > 1;
}

/// <summary>
/// Device descriptions from the directories named in machine.yml ("deviceDescriptions", E-015).
/// Match is exact on Type, Id (case-insensitive hex) and Version; two descriptions with the same
/// identification are ambiguous and give nothing (in the fixtures PCF8575IN and SSD1306 share one).
/// </summary>
public sealed class DeviceDescriptionIndex
{
    /// <summary>Order number the fixtures write when there is none.</summary>
    public const string NoOrderNumber = "-";

    private readonly List<DeviceDescription> descriptions;

    public DeviceDescriptionIndex(IEnumerable<DeviceDescription> descriptions)
    {
        this.descriptions = descriptions.OrderBy(d => d.File, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<DeviceDescription> All => descriptions;

    /// <summary>
    /// Reads every *.devdesc.xml below the directories (recursive, sorted). Each entry pairs the
    /// absolute directory with the path written into the SBOM (relative to the repository, '/').
    /// </summary>
    public static DeviceDescriptionIndex Load(IEnumerable<(string FullPath, string DisplayPath)> directories)
    {
        var result = new List<DeviceDescription>();
        foreach (var (fullPath, displayPath) in directories)
        {
            if (!Directory.Exists(fullPath))
            {
                throw new UsageException("DEVDESC_DIR_NOT_FOUND", $"Das Verzeichnis {displayPath} aus „deviceDescriptions“ gibt es nicht.");
            }
            var files = Directory.EnumerateFiles(fullPath, "*.devdesc.xml", SearchOption.AllDirectories)
                .Select(f => (Full: f, Relative: Path.GetRelativePath(fullPath, f).Replace('\\', '/')))
                .OrderBy(f => f.Relative, StringComparer.Ordinal);
            foreach (var (full, relative) in files)
            {
                var display = $"{displayPath.TrimEnd('/')}/{relative}";
                result.AddRange(Read(File.ReadAllBytes(full), display));
            }
        }
        return new DeviceDescriptionIndex(result);
    }

    /// <summary>Reads the Device elements of one file.</summary>
    public static IEnumerable<DeviceDescription> Read(byte[] content, string displayPath)
    {
        var root = SafeXml.Load(content, displayPath).Root!;
        if (root.Name.LocalName != "DeviceDescription")
        {
            throw new UsageException(
                "NOT_DEVDESC",
                $"Die Datei {displayPath} ist keine CODESYS-Gerätebeschreibung (Wurzelelement „{root.Name.LocalName}“ statt „DeviceDescription“).");
        }
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        return root.ElementsNamed("Device").Select(device =>
        {
            var identification = device.ElementNamed("DeviceIdentification");
            var info = device.ElementNamed("DeviceInfo");
            var orderNumber = info?.ChildText("OrderNumber");
            return new DeviceDescription(
                displayPath,
                sha256,
                identification?.ChildText("Type"),
                identification?.ChildText("Id"),
                identification?.ChildText("Version"),
                info?.ChildText("Name"),
                info?.ChildText("Vendor"),
                orderNumber == NoOrderNumber ? null : orderNumber);
        }).ToList();
    }

    public DeviceDescriptionMatch Find(string? type, string? id, string? version)
    {
        if (type is null || id is null || version is null)
        {
            return new DeviceDescriptionMatch(null, []);
        }
        var candidates = descriptions
            .Where(d => d.Type == type
                && string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)
                && d.Version == version)
            .ToList();
        return new DeviceDescriptionMatch(candidates.Count == 1 ? candidates[0] : null, candidates);
    }
}
