using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PlcSbom.Core.Adapters.Gsdml;

// PROFINET GSDML file (CLAUDE.md section 5.4.5, docs/adapter-tia-aml.md section 5.4). Element and attribute
// names as in fixtures/gsdml/GSDML-V2.41-LEUZE-BCL248i-20211213.xml: ISO15745Profile/ProfileBody with
// DeviceIdentity (VendorID, DeviceID, VendorName@Value) and ApplicationProcess with
// DeviceAccessPointList/DeviceAccessPointItem and ModuleList/ModuleItem, each with a direct ModuleInfo
// (OrderNumber, SoftwareRelease, HardwareRelease as @Value). Used by tia-aml and later tia-portal.

/// <summary>A device access point or module of a GSDML file with the facts of its ModuleInfo.</summary>
public sealed record GsdmlItem(string Id, string? OrderNumber, string? SoftwareRelease, string? HardwareRelease);

/// <summary>One GSDML file: identity, vendor and the items TIA refers to by id.</summary>
public sealed record GsdmlFile(
    string File,
    string Sha256,
    string? VendorId,
    string? DeviceId,
    string? VendorName,
    IReadOnlyDictionary<string, GsdmlItem> AccessPoints,
    IReadOnlyDictionary<string, GsdmlItem> Modules)
{
    /// <summary>
    /// The order number stands for several articles: it contains '*', '?' or a run of lowercase 'x'
    /// (fixture: Leuze "501xxxxx"). A single uppercase X is part of real order numbers (6ES7 214-1AG40-0XB0).
    /// </summary>
    public static bool IsOrderNumberPattern(string orderNumber) => GsdmlReader.PatternRegex().IsMatch(orderNumber);
}

/// <summary>Result of looking up a GSDML file by name.</summary>
public sealed record GsdmlLookup(GsdmlFile? Found, IReadOnlyList<string> Candidates)
{
    /// <summary>Several files with that name and different content: none is taken.</summary>
    public bool IsAmbiguous => Found is null && Candidates.Count > 1;
}

public static partial class GsdmlReader
{
    public const string RootElement = "ISO15745Profile";

    public static GsdmlFile Read(byte[] content, string displayPath)
    {
        var root = SafeXml.Load(content, displayPath).Root!;
        var body = root.Name.LocalName == RootElement ? root.ElementNamed("ProfileBody") : null;
        var identity = body?.ElementNamed("DeviceIdentity");
        if (identity is null)
        {
            throw new UsageException(
                "NOT_GSDML",
                $"Die Datei {displayPath} ist keine GSDML-Datei (erwartet: Wurzelelement „{RootElement}“ mit ProfileBody/DeviceIdentity, gefunden: „{root.Name.LocalName}“).");
        }
        var process = body!.ElementNamed("ApplicationProcess");
        return new GsdmlFile(
            displayPath,
            Convert.ToHexStringLower(SHA256.HashData(content)),
            identity.AttributeValue("VendorID"),
            identity.AttributeValue("DeviceID"),
            identity.ElementNamed("VendorName")?.AttributeValue("Value"),
            Items(process?.ElementNamed("DeviceAccessPointList"), "DeviceAccessPointItem"),
            Items(process?.ElementNamed("ModuleList"), "ModuleItem"));
    }

    /// <summary>Items by ID; only the direct ModuleInfo counts (submodules carry their own). First of a duplicate id wins.</summary>
    private static Dictionary<string, GsdmlItem> Items(System.Xml.Linq.XElement? list, string elementName)
    {
        var result = new Dictionary<string, GsdmlItem>(StringComparer.Ordinal);
        foreach (var element in list?.ElementsNamed(elementName) ?? [])
        {
            var id = element.AttributeValue("ID");
            if (id is null || result.ContainsKey(id))
            {
                continue;
            }
            var info = element.ElementNamed("ModuleInfo");
            result[id] = new GsdmlItem(
                id,
                info?.ElementNamed("OrderNumber")?.AttributeValue("Value")?.Trim(),
                info?.ElementNamed("SoftwareRelease")?.AttributeValue("Value")?.Trim(),
                info?.ElementNamed("HardwareRelease")?.AttributeValue("Value")?.Trim());
        }
        return result;
    }

    [GeneratedRegex(@"[*?]|xx", RegexOptions.CultureInvariant)]
    internal static partial Regex PatternRegex();
}

/// <summary>
/// GSDML files below the directories from machine.yml "gsdml" (recursive). TIA names the file in the
/// TypeIdentifier in upper case (GSDML-V2.41-LEUZE-BCL248I-20211213.XML), the vendor ships it in mixed case,
/// so names are compared without case. Files are parsed on first use.
/// </summary>
public sealed class GsdmlIndex
{
    private readonly ILookup<string, (string Full, string Display)> files;

    private readonly Dictionary<string, GsdmlFile> parsed = new(StringComparer.Ordinal);

    private GsdmlIndex(IEnumerable<(string Full, string Display)> files)
    {
        this.files = files
            .OrderBy(f => f.Display, StringComparer.Ordinal)
            .ToLookup(f => Path.GetFileName(f.Full), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Each entry pairs the absolute directory with the path written into the SBOM (relative to the repository, '/').</summary>
    public static GsdmlIndex Load(IEnumerable<(string FullPath, string DisplayPath)> directories)
    {
        var all = new List<(string, string)>();
        foreach (var (fullPath, displayPath) in directories)
        {
            if (!Directory.Exists(fullPath))
            {
                throw new UsageException("GSDML_DIR_NOT_FOUND", $"Das Verzeichnis {displayPath} aus „gsdml“ gibt es nicht.");
            }
            all.AddRange(Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                .Select(f => (f, $"{displayPath.TrimEnd('/', '\\')}/{Path.GetRelativePath(fullPath, f).Replace('\\', '/')}")));
        }
        return new GsdmlIndex(all);
    }

    /// <summary>
    /// The file with this name. Several hits with the same content: the first by path. Several with
    /// different content: ambiguous, nothing is taken (which one TIA used is unknown).
    /// </summary>
    public GsdmlLookup Find(string fileName)
    {
        var hits = files[fileName].Select(Parse).ToList();
        if (hits.Count == 0)
        {
            return new GsdmlLookup(null, []);
        }
        var candidates = hits.Select(h => h.File).ToList();
        return hits.Select(h => h.Sha256).Distinct(StringComparer.Ordinal).Count() == 1
            ? new GsdmlLookup(hits[0], candidates)
            : new GsdmlLookup(null, candidates);
    }

    private GsdmlFile Parse((string Full, string Display) file)
    {
        if (!parsed.TryGetValue(file.Full, out var result))
        {
            result = GsdmlReader.Read(File.ReadAllBytes(file.Full), file.Display);
            parsed[file.Full] = result;
        }
        return result;
    }
}
