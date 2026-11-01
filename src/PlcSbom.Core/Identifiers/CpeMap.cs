using System.Text;
using PlcSbom.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace PlcSbom.Core.Identifiers;

/// <summary>One row of the CPE table: vendor, order number pattern ('*', '?') and CPE with {version}.</summary>
public sealed record CpeMapEntry(string Vendor, string OrderNumber, string Cpe);

/// <summary>
/// CPE table kennungen/cpe-map.yml (CLAUDE.md section 6), maintained by the user. A CPE is set only
/// when a row matches vendor and order number and the firmware version is known; it is never derived
/// from the type name. {version} is the firmware without a leading "V".
/// </summary>
public sealed class CpeMap
{
    public const string VersionPlaceholder = "{version}";

    private static readonly string[] Keys = ["vendor", "orderNumber", "cpe"];

    public CpeMap(IReadOnlyList<CpeMapEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<CpeMapEntry> Entries { get; }

    public static CpeMap Load(string fullPath, string displayPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new UsageException("CPE_MAP_NOT_FOUND", $"Die CPE-Tabelle {displayPath} aus „cpeMap“ gibt es nicht.");
        }
        return Parse(File.ReadAllText(fullPath), displayPath);
    }

    public static CpeMap Parse(string text, string displayPath)
    {
        UsageException Error(Mark? at, string message, Exception? inner = null) =>
            new("CPE_MAP_INVALID", at is { Line: > 0 } mark ? $"{displayPath}, Zeile {mark.Line}: {message}" : $"{displayPath}: {message}", inner);

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException ex)
        {
            throw Error(ex.Start, $"Kein gültiges YAML: {ex.Message}", ex);
        }
        if (stream.Documents.Count == 0)
        {
            return new CpeMap([]);
        }
        if (stream.Documents[0].RootNode is not YamlSequenceNode rows)
        {
            throw Error(stream.Documents[0].RootNode.Start, "Die CPE-Tabelle muss eine Liste von Einträgen mit vendor, orderNumber und cpe sein.");
        }
        var entries = new List<CpeMapEntry>();
        foreach (var row in rows.Children)
        {
            if (row is not YamlMappingNode map)
            {
                throw Error(row.Start, "Jeder Eintrag der CPE-Tabelle braucht vendor, orderNumber und cpe.");
            }
            foreach (var key in map.Children.Keys)
            {
                if (key is not YamlScalarNode { Value: { } name } || !Keys.Contains(name, StringComparer.Ordinal))
                {
                    throw Error(key.Start, $"Unbekannter Schlüssel „{(key as YamlScalarNode)?.Value}“. Erlaubt sind: {string.Join(", ", Keys)}.");
                }
            }
            string Value(string key) =>
                map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode { Value: { } v } && v.Trim().Length > 0
                    ? v.Trim()
                    : throw Error(map.Start, $"„{key}“ fehlt im Eintrag.");
            var cpe = Value("cpe");
            if (!cpe.StartsWith("cpe:2.3:", StringComparison.Ordinal) || !cpe.Contains(VersionPlaceholder, StringComparison.Ordinal))
            {
                throw Error(map.Start, $"„cpe“ muss eine CPE 2.3 mit dem Platzhalter {VersionPlaceholder} sein, gefunden „{cpe}“. Eine feste Version würde jeder Firmware-Version dieselbe CPE geben.");
            }
            entries.Add(new CpeMapEntry(Value("vendor"), Value("orderNumber"), cpe));
        }
        return new CpeMap(entries);
    }

    /// <summary>The CPE of a device's firmware, or null if no row matches or the firmware is unknown.</summary>
    public string? Find(Device device)
    {
        if (device.Vendor is null || device.OrderNumber is null || device.Firmware is null)
        {
            return null;
        }
        var entry = Entries.FirstOrDefault(e => e.Vendor == device.Vendor.Name && Wildcard.IsOrderNumberMatch(e.OrderNumber, device.OrderNumber));
        return entry?.Cpe.Replace(VersionPlaceholder, Escape(VersionWithoutPrefix(device.Firmware)), StringComparison.Ordinal);
    }

    /// <summary>"V2.8" → "2.8"; "4.7.10" stays.</summary>
    public static string VersionWithoutPrefix(string firmware) =>
        firmware.Length > 1 && firmware[0] is 'V' or 'v' && char.IsDigit(firmware[1]) ? firmware[1..] : firmware;

    /// <summary>CPE 2.3 formatted string: characters other than letters, digits, '.', '_' and '-' are quoted with '\'.</summary>
    internal static string Escape(string value)
    {
        var b = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '-')
            {
                b.Append('\\');
            }
            b.Append(c);
        }
        return b.ToString();
    }
}
