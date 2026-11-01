using PlcSbom.Core.Sbom;

namespace PlcSbom.Core.FieldState;

/// <summary>Result per device (CLAUDE.md section 8.3).</summary>
public enum ComparisonStatus
{
    /// <summary>gleich: matched, nothing differs.</summary>
    Equal,

    /// <summary>abweichend: matched, order number or firmware differ.</summary>
    Different,

    /// <summary>nur-im-projekt: in the SBOM, not found in the field file.</summary>
    ProjectOnly,

    /// <summary>nur-im-feld: in the field file, no device of the SBOM matches.</summary>
    FieldOnly,

    /// <summary>passiv: in the field file with firmware 0.0 and no device of the SBOM matches. Not a deviation (E-023).</summary>
    Passive,
}

/// <summary>One line of the comparison: an SBOM device, a field device, or both.</summary>
public sealed record ComparisonEntry
{
    public required ComparisonStatus Status { get; init; }

    public SbomComponent? Sbom { get; init; }

    public FieldDevice? Field { get; init; }

    /// <summary>"position" or "orderNumber" for matched entries.</summary>
    public string? MatchedBy { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string? Station => Sbom?.Station ?? Field?.Station;

    public string? Position => Sbom?.Own("device:position") ?? Field?.Position;

    /// <summary>Type name of the SBOM device, otherwise what the field file says.</summary>
    public string Device => Sbom?.Own("device:type") ?? Field?.OrderNumber ?? Field?.Label ?? "";

    public string? SbomOrderNumber => Sbom?.Own("device:orderNumber");

    public string? SbomFirmware => Sbom is null ? null : Sbom.Version ?? Sbom.Own("firmware:version");

    /// <summary>Status as written in console, report and CSV.</summary>
    public string StatusText => Text(Status);

    public static string Text(ComparisonStatus status) => status switch
    {
        ComparisonStatus.Equal => "gleich",
        ComparisonStatus.Different => "abweichend",
        ComparisonStatus.ProjectOnly => "nur-im-projekt",
        ComparisonStatus.FieldOnly => "nur-im-feld",
        ComparisonStatus.Passive => "passiv",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}

/// <summary>Comparison of one SBOM with one field file.</summary>
public sealed record Comparison
{
    public required AsFoundState Field { get; init; }

    public required IReadOnlyList<ComparisonEntry> Entries { get; init; }

    public int Count(ComparisonStatus status) => Entries.Count(e => e.Status == status);

    /// <summary>Devices of the SBOM that were looked for (every device of the SBOM).</summary>
    public int Checked => Entries.Count(e => e.Sbom is not null);

    /// <summary>abweichend, nur-im-projekt and nur-im-feld. Each one is exit 1.</summary>
    public int Deviations => Count(ComparisonStatus.Different) + Count(ComparisonStatus.ProjectOnly) + Count(ComparisonStatus.FieldOnly);
}

/// <summary>
/// Compares the projected state of an SBOM with an as-found state (CLAUDE.md section 8.3). Devices are the
/// firmware components with device properties. Matching first over station and position, then over the
/// order number without spaces; neither side is changed. Firmware versions are compared without a leading
/// "V"; a version that is more precise than the other but starts the same (V2.8 and V2.8.3) counts as equal,
/// because TIA projects only major and minor (E-023).
/// </summary>
public static class FieldComparer
{
    public static Comparison Compare(SbomView sbom, AsFoundState field)
    {
        if (sbom.Namespace is null)
        {
            throw new UsageException("SBOM_NOT_PLC_SBOM",
                "Die SBOM enthält keine Properties von plc-sbom (Station, Position, Bestellnummer). Der Abgleich braucht eine SBOM aus plc-sbom build.");
        }
        if (sbom.Primary?.Name is { } name && name != field.Machine)
        {
            throw new UsageException("FIELD_MACHINE_MISMATCH",
                $"Die Feld-Datei {field.SourcePath} gehört zur Maschine „{field.Machine}“, die SBOM beschreibt „{name}“.");
        }

        var devices = sbom.Components.Where(c => c.IsDevice).ToList();
        var stations = devices.Select(d => d.Station).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var matched = new Dictionary<SbomComponent, (FieldDevice Field, string By)>();
        var open = field.Devices.ToList();

        bool InStation(SbomComponent device, FieldDevice f) => f.Station is null || f.Station == device.Station;

        // 1. Station and position.
        foreach (var f in open.ToList().Where(f => f.Position is not null))
        {
            var candidates = devices.Where(d => !matched.ContainsKey(d) && d.Own("device:position") == f.Position && InStation(d, f)).ToList();
            if (candidates.Count == 1)
            {
                matched[candidates[0]] = (f, "position");
                open.Remove(f);
            }
        }

        // 2. Order number; among equal order numbers the one with the same firmware first.
        foreach (var f in open.ToList().Where(f => f.OrderNumber is not null))
        {
            var candidates = devices
                .Where(d => !matched.ContainsKey(d) && InStation(d, f) && SameOrderNumber(d, f.OrderNumber!))
                .ToList();
            var pick = candidates.FirstOrDefault(d => FirmwareVersions.Compare(Firmware(d), f.Firmware) == VersionMatch.Equal)
                ?? candidates.FirstOrDefault();
            if (pick is not null)
            {
                matched[pick] = (f, "orderNumber");
                open.Remove(f);
            }
        }

        var entries = new List<ComparisonEntry>();
        foreach (var device in devices)
        {
            entries.Add(matched.TryGetValue(device, out var m)
                ? Matched(device, m.Field, m.By)
                : new ComparisonEntry
                {
                    Status = ComparisonStatus.ProjectOnly,
                    Sbom = device,
                    Notes = ["Im Ist-Stand nicht gefunden."],
                });
        }
        foreach (var f in open)
        {
            var notes = new List<string>();
            if (f.Station is not null && !stations.Contains(f.Station))
            {
                notes.Add($"Die Station {f.Station} gibt es in der SBOM nicht.");
            }
            var passive = FirmwareVersions.IsZero(f.Firmware);
            notes.Add(passive
                ? "Firmware 0.0: Gerät ohne eigene Firmware, steht nicht in der SBOM."
                : "Kein Gerät der SBOM passt (Station und Position, Bestellnummer).");
            entries.Add(new ComparisonEntry
            {
                Status = passive ? ComparisonStatus.Passive : ComparisonStatus.FieldOnly,
                Field = f,
                Notes = notes,
            });
        }
        return new Comparison { Field = field, Entries = entries };
    }

    private static ComparisonEntry Matched(SbomComponent device, FieldDevice f, string by)
    {
        var notes = new List<string>();
        var different = false;

        var orderNumber = device.Own("device:orderNumber");
        if (orderNumber is not null && f.OrderNumber is not null && !SameOrderNumber(device, f.OrderNumber))
        {
            different = true;
            notes.Add($"Bestellnummer im Feld {f.OrderNumber}, in der SBOM {orderNumber}.");
        }

        var firmware = Firmware(device);
        switch (FirmwareVersions.Compare(firmware, f.Firmware))
        {
            case VersionMatch.Different:
                different = true;
                notes.Add($"Firmware im Feld {f.Firmware}, in der SBOM {firmware}.");
                break;
            case VersionMatch.Refined:
                notes.Add($"Firmware im Feld {f.Firmware} genauer als in der SBOM ({firmware}).");
                break;
            case VersionMatch.OnlyField:
                notes.Add($"Firmware {f.Firmware} nur im Feld bekannt, die SBOM führt keine.");
                break;
            case VersionMatch.OnlySbom:
                notes.Add("Firmware im Feld nicht ausgelesen.");
                break;
            case VersionMatch.Equal:
            case VersionMatch.None:
                break;
        }

        // Web API: the configuration loaded into the CPU is a second projected state.
        if (f.ConfiguredOrderNumber is { } configuredOrder && orderNumber is not null && !SameOrderNumber(device, configuredOrder))
        {
            different = true;
            notes.Add($"In der CPU projektiert ist Bestellnummer {configuredOrder}, nicht {orderNumber}.");
        }
        if (f.ConfiguredFirmware is { } configuredFirmware && firmware is not null
            && FirmwareVersions.Compare(firmware, configuredFirmware) == VersionMatch.Different)
        {
            different = true;
            notes.Add($"In der CPU projektiert ist Firmware {configuredFirmware}, nicht {firmware}; das geladene Projekt weicht vom Stand der SBOM ab.");
        }

        return new ComparisonEntry
        {
            Status = different ? ComparisonStatus.Different : ComparisonStatus.Equal,
            Sbom = device,
            Field = f,
            MatchedBy = by,
            Notes = notes,
        };
    }

    private static string? Firmware(SbomComponent device) => device.Version ?? device.Own("firmware:version");

    /// <summary>Order numbers without spaces; an SBOM order number with placeholders (6ES7 590-1***0-0AA0) is a pattern.</summary>
    private static bool SameOrderNumber(SbomComponent device, string fieldOrderNumber)
    {
        if (device.Own("device:orderNumber") is not { } orderNumber)
        {
            return false;
        }
        return device.Own("device:orderNumber:pattern") == "true"
            ? Wildcard.IsOrderNumberMatch(orderNumber.Replace("x", "?", StringComparison.Ordinal), fieldOrderNumber)
            : string.Equals(Compact(orderNumber), Compact(fieldOrderNumber), StringComparison.OrdinalIgnoreCase);
    }

    internal static string Compact(string orderNumber) => orderNumber.Replace(" ", "", StringComparison.Ordinal);
}

public enum VersionMatch
{
    /// <summary>Neither side has a version.</summary>
    None,
    Equal,

    /// <summary>One version continues the other (V2.8 and V2.8.3).</summary>
    Refined,
    Different,
    OnlySbom,
    OnlyField,
}

/// <summary>Firmware versions as written by the sources: "V2.8", "4.7.10", "V4.1.0".</summary>
public static class FirmwareVersions
{
    public static VersionMatch Compare(string? sbom, string? field)
    {
        if (sbom is null || field is null)
        {
            return sbom is null && field is null ? VersionMatch.None : sbom is null ? VersionMatch.OnlyField : VersionMatch.OnlySbom;
        }
        var a = Parts(sbom);
        var b = Parts(field);
        if (a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase))
        {
            return VersionMatch.Equal;
        }
        var shorter = a.Count < b.Count ? a : b;
        var longer = a.Count < b.Count ? b : a;
        return longer.Take(shorter.Count).SequenceEqual(shorter, StringComparer.OrdinalIgnoreCase) ? VersionMatch.Refined : VersionMatch.Different;
    }

    /// <summary>V0.0, 0.0.0: the source states that the device has no firmware of its own.</summary>
    public static bool IsZero(string? version) =>
        version is not null && Parts(version) is { Count: > 0 } parts && parts.All(p => p == "0");

    /// <summary>Without a leading "V" before a digit; numeric parts without leading zeros.</summary>
    internal static List<string> Parts(string version)
    {
        var text = version.Trim();
        if (text.Length > 1 && text[0] is 'V' or 'v' && char.IsAsciiDigit(text[1]))
        {
            text = text[1..];
        }
        return text.Split('.').Select(p => p.All(char.IsAsciiDigit) && p.Length > 0 ? p.TrimStart('0') is { Length: > 0 } t ? t : "0" : p).ToList();
    }
}
