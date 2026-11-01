using System.Globalization;
using PlcSbom.Core.FieldState;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Core.Sbom;
using PlcSbom.Core.Validation;

namespace PlcSbom.Core.Output.Report;

/// <summary>A device of the hardware appendix: passive, rack or left out with ignoreDevices.</summary>
public sealed record HardwareItem(string? Station, Device Device);

/// <summary>
/// What only build knows and the SBOM does not carry (ADR E-007, E-022): passive modules and racks,
/// devices left out on purpose, the engineering tool of each station.
/// </summary>
public sealed record ReportExtras
{
    public IReadOnlyDictionary<string, ToolInfo> StationTools { get; init; } = new Dictionary<string, ToolInfo>();

    public IReadOnlyList<HardwareItem> Hardware { get; init; } = [];

    public IReadOnlyList<HardwareItem> Ignored { get; init; } = [];

    public static ReportExtras From(Machine machine, IEnumerable<Device> ignored) => new()
    {
        StationTools = machine.Stations
            .Where(s => s.EngineeringTool is not null)
            .ToDictionary(s => s.Name, s => s.EngineeringTool!, StringComparer.Ordinal),
        Hardware = machine.Stations
            .SelectMany(s => s.Devices.Where(d => d.IsPassive).Select(d => new HardwareItem(s.Name, d)))
            .Concat(machine.UnassignedDevices.Where(d => d.IsPassive).Select(d => new HardwareItem(null, d)))
            .ToList(),
        Ignored = ignored.Select(d => new HardwareItem(null, d)).ToList(),
    };
}

/// <summary>Everything a report is made of.</summary>
public sealed record ReportInput
{
    public required SbomView Sbom { get; init; }

    /// <summary>SBOM file as named in the report, with '/'.</summary>
    public required string SbomPath { get; init; }

    public required ValidationReport Validation { get; init; }

    public bool ReleaseBuild { get; init; }

    /// <summary>From build; null when the report is made from the SBOM alone.</summary>
    public ReportExtras? Extras { get; init; }

    public Comparison? Comparison { get; init; }
}

/// <summary>
/// Prüfbericht (CLAUDE.md section 8.2): head, key figures, sources, tree machine → station → component,
/// a table per station, gaps with what closes them, further findings, optional comparison with the field,
/// hardware appendix. Made from the SBOM and the validation, so report and build show the same; build adds
/// what the SBOM does not carry (<see cref="ReportExtras"/>). No clock: the time shown is the SBOM's.
/// </summary>
public static class ReportBuilder
{
    private const string Dash = "–";

    public static ReportDocument Build(ReportInput input)
    {
        var sbom = input.Sbom;
        var machine = sbom.Primary;
        var blocks = new List<Block>();
        var passed = input.Validation.ExitCode(input.ReleaseBuild) == ExitCodes.Ok;
        var buildKind = input.ReleaseBuild ? "Release-Build" : "Entwicklungs-Build";
        var verdict = $"{(passed ? "bestanden" : "nicht bestanden")} ({buildKind})";
        var gapCodes = GapCatalog.All.ToDictionary(g => TrValidator.ToCode(g.Code), g => g.Code, StringComparer.Ordinal);
        var stations = sbom.Components.Select(c => c.Station).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        // ---- Head ----
        var head = new List<KeyValuePair<string, string>>
        {
            new("Maschine", machine?.Name ?? Dash),
            new("Release", machine?.Version ?? Dash),
        };
        if (machine?.Own("git:commit") is { } commit)
        {
            head.Add(new("Git-Commit", commit));
        }
        if (machine?.Own("machine:serialNumber") is { } serial)
        {
            head.Add(new("Seriennummer der Maschine", serial));
        }
        head.Add(new("Ersteller der SBOM", Party(sbom.Creator)));
        head.Add(new("Datei", input.SbomPath));
        head.Add(new("Format", $"CycloneDX {sbom.SpecVersion ?? "?"}, JSON"));
        head.Add(new("SBOM-Kennung", sbom.SerialNumber ?? Dash));
        head.Add(new("Erzeugt", sbom.Timestamp is { } t ? t.ToUniversalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC" : Dash));
        if (sbom.Tools.FirstOrDefault(tool => tool.Name == "PLC SBOM Builder") is { } self)
        {
            head.Add(new("Erzeugt mit", $"PLC SBOM Builder {self.Version}"));
        }
        head.Add(new("Prüfstatus", verdict));
        head.Add(new("Befunde", $"{Count(input.Validation.Errors, "Fehler", "Fehler")}, {Count(input.Validation.Warnings, "Warnung", "Warnungen")}, {Count(input.Validation.Notes, "Hinweis", "Hinweise")}"));
        blocks.Add(new Facts(head));

        // ---- Key figures ----
        var vendorSboms = sbom.Components.Where(c => c.Type == "firmware" && c.InputSbom is not null).Select(c => c.InputSbom!).Distinct(StringComparer.Ordinal).Count();
        var partialRoots = sbom.Components.Where(c => c.Own("inputSbom:kind") == "partial").ToList();
        blocks.Add(new Heading(2, "Kennzahlen"));
        blocks.Add(new Facts(
        [
            new("Komponenten", (input.Validation.Components).ToString(CultureInfo.InvariantCulture) + " (mit der Maschine)"),
            new("Stationen", stations.Count.ToString(CultureInfo.InvariantCulture)),
            new("Firmware-Komponenten", sbom.Components.Count(c => c.Type == "firmware" && c.InputSbom is null).ToString(CultureInfo.InvariantCulture)),
            new("Hersteller-SBOMs", vendorSboms.ToString(CultureInfo.InvariantCulture)),
            new("Teil-SBOMs", partialRoots.Count.ToString(CultureInfo.InvariantCulture)),
            new("Komponenten ohne Hash", sbom.Components.Count(c => !c.HasDistributionHash).ToString(CultureInfo.InvariantCulture)),
        ]));

        // ---- Sources ----
        blocks.Add(new Heading(2, "Quellen"));
        var sourceRows = new List<IReadOnlyList<string>>();
        foreach (var station in stations)
        {
            var first = sbom.Components.FirstOrDefault(c => c.Station == station && c.Own("adapter") is not null);
            var tool = input.Extras?.StationTools.GetValueOrDefault(station);
            var adapter = first?.Own("adapter") is { } a ? first.Own("adapter:source") is { } s ? $"{a} ({s})" : a : Dash;
            sourceRows.Add(
            [
                station,
                adapter,
                first?.Own("source") ?? Dash,
                first?.Own("source:sha256") ?? Dash,
                tool is null ? Dash : Tool(tool.Name, tool.Version, tool.Release),
                first?.Own("source:exportedAt") is { } exported && DateTimeOffset.TryParse(exported, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                    ? at.ToUniversalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC"
                    : Dash,
            ]);
        }
        if (sourceRows.Count > 0)
        {
            blocks.Add(new Table(["Station", "Adapter", "Datei", "SHA-256", "Engineering-Werkzeug", "Export"], sourceRows));
        }
        else
        {
            blocks.Add(new Paragraph("Die SBOM nennt keine Stationen von plc-sbom."));
        }
        if (input.Extras is null && sourceRows.Count > 0)
        {
            blocks.Add(new Paragraph("Die SBOM ordnet die Engineering-Werkzeuge keiner Station zu; sie stehen unter „Werkzeuge“. Der Bericht aus plc-sbom build nennt sie je Station."));
        }

        var inputRows = new List<IReadOnlyList<string>>();
        foreach (var firmware in sbom.Components.Where(c => c.Type == "firmware" && c.InputSbom is not null))
        {
            inputRows.Add([firmware.InputSbom!, "Hersteller-SBOM", firmware.Own("inputSbom:sha256") ?? Dash, $"{firmware.Name} ({firmware.BomRef})"]);
        }
        foreach (var root in partialRoots)
        {
            inputRows.Add([root.InputSbom ?? Dash, "Teil-SBOM", root.Own("inputSbom:sha256") ?? Dash, root.Station is { } st ? $"Station {st}" : "Maschine"]);
        }
        if (inputRows.Count > 0)
        {
            blocks.Add(new Heading(3, "Eingelesene SBOMs"));
            blocks.Add(new Table(["Datei", "Art", "SHA-256", "Zugeordnet zu"], inputRows));
        }

        blocks.Add(new Heading(3, "Werkzeuge"));
        blocks.Add(new Table(["Werkzeug", "Version", "Hersteller"],
            sbom.Tools.Select(tool => (IReadOnlyList<string>)[tool.Name, Tool("", tool.Version ?? Dash, tool.Release).Trim(), tool.Vendor ?? Dash]).ToList()));

        // ---- Tree ----
        blocks.Add(new Heading(2, "Maschinenbaum"));
        blocks.Add(new Tree(MachineTree(input, stations)));

        // ---- Components per station ----
        var findingsByRef = input.Validation.Findings
            .Where(f => f.ComponentRef is not null)
            .GroupBy(f => f.ComponentRef!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        blocks.Add(new Heading(2, "Komponenten je Station"));
        foreach (var station in stations)
        {
            blocks.Add(new Heading(3, $"Station {station}"));
            blocks.Add(ComponentTable(sbom.Components.Where(c => c.Station == station), findingsByRef, gapCodes));
        }
        var withoutStation = sbom.Components.Where(c => c.Station is null && (c.InputSbom is null || c.Own("inputSbom:kind") == "partial")).ToList();
        if (withoutStation.Count > 0)
        {
            blocks.Add(new Heading(3, "Ohne Station"));
            blocks.Add(ComponentTable(withoutStation, findingsByRef, gapCodes));
        }
        foreach (var group in sbom.Components
            .Where(c => c.InputSbom is not null && c.Own("inputSbom:kind") is null && c.Type != "firmware" && c.Station is null)
            .GroupBy(c => c.InputSbom!, StringComparer.Ordinal))
        {
            blocks.Add(new Heading(3, $"Aus {group.Key}"));
            blocks.Add(ComponentTable(group, findingsByRef, gapCodes));
        }

        // ---- Gaps ----
        blocks.Add(new Heading(2, "Lücken"));
        var gapRows = input.Validation.Findings
            .Where(f => gapCodes.ContainsKey(f.Code))
            .GroupBy(f => gapCodes[f.Code], StringComparer.Ordinal)
            .OrderByDescending(g => g.First().Severity)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (IReadOnlyList<string>)
            [
                g.Key,
                SeverityText(g.First().Severity),
                g.Count().ToString(CultureInfo.InvariantCulture),
                string.Join(", ", g.Select(f => f.ComponentRef ?? Dash).Distinct(StringComparer.Ordinal)),
                GapCatalog.Find(g.Key)?.Description ?? Dash,
                GapCatalog.Remedy(g.Key) ?? Dash,
            ])
            .ToList();
        blocks.Add(gapRows.Count == 0
            ? new Paragraph("Keine Lücken vermerkt.")
            : new Table(["Code", "Schwere", "Anzahl", "Komponenten", "Erklärung", "Was sie schließt"], gapRows));

        // ---- Further findings ----
        var others = input.Validation.Findings.Where(f => !gapCodes.ContainsKey(f.Code)).ToList();
        if (others.Count > 0)
        {
            blocks.Add(new Heading(2, "Weitere Befunde"));
            blocks.Add(new Table(["Schwere", "Code", "Komponente", "Meldung"],
                others.Select(f => (IReadOnlyList<string>)[SeverityText(f.Severity), f.Code, f.ComponentRef ?? Dash, f.Message]).ToList()));
        }

        // ---- Field ----
        if (input.Comparison is { } comparison)
        {
            AddComparison(blocks, comparison);
        }

        // ---- Hardware appendix ----
        blocks.Add(new Heading(2, "Anhang: Hardware ohne eigene Firmware"));
        if (input.Extras is null)
        {
            blocks.Add(new Paragraph("Passive Module und Racks stehen nicht in der SBOM, weil sie keine Software enthalten. Diesen Anhang schreibt nur plc-sbom build, das die Projekte selbst liest."));
        }
        else
        {
            blocks.Add(input.Extras.Hardware.Count == 0
                ? new Paragraph("Keine.")
                : HardwareTable(input.Extras.Hardware, withReason: true));
            if (input.Extras.Ignored.Count > 0)
            {
                blocks.Add(new Heading(3, "Ausgeschlossene Geräte (ignoreDevices)"));
                blocks.Add(HardwareTable(input.Extras.Ignored, withReason: false));
            }
        }

        blocks.Add(new Paragraph($"Bericht von PLC SBOM Builder zu {input.SbomPath}. Eine SBOM allein erfüllt den Cyber Resilience Act nicht; Schwachstellen bewertet das Werkzeug nicht."));
        var title = machine is null ? "Prüfbericht" : $"Prüfbericht {machine.Name}, {machine.Version}";
        return new ReportDocument(title, passed, verdict, blocks);
    }

    private static TreeNode MachineTree(ReportInput input, IReadOnlyList<string> stations)
    {
        var sbom = input.Sbom;
        var children = new List<TreeNode>();
        foreach (var station in stations)
        {
            var members = sbom.Components.Where(c => c.Station == station).ToList();
            var programs = members.Where(c => c.SoftwareKind == "user-program").ToList();
            var controllerRefs = programs.SelectMany(p => sbom.DependsOn.GetValueOrDefault(p.BomRef ?? "") ?? []).ToHashSet(StringComparer.Ordinal);
            var firmware = members.Where(c => c.Type == "firmware").ToList();
            var controllers = firmware.Where(f => f.BomRef is not null && controllerRefs.Contains(f.BomRef)).ToList();
            var groups = new List<TreeNode>();
            Group(groups, "Steuerung", controllers.Select(c => FirmwareNode(sbom, c)));
            Group(groups, "Anwenderprogramm", programs.Select(Node));
            Group(groups, "E/A-Module und Feldgeräte", firmware.Except(controllers).Select(c => FirmwareNode(sbom, c)), counted: true);
            Group(groups, "Laufzeit", members.Where(c => c.SoftwareKind == "runtime").Select(Node));
            Group(groups, "Bibliotheken", members.Where(c => c.SoftwareKind is "library" or "library-type" or "add-on-instruction" or "protected-block").Select(Node), counted: true);
            Group(groups, "Teil-SBOMs", members.Where(c => c.Own("inputSbom:kind") == "partial").Select(c => PartialNode(sbom, c)));
            if (input.Extras is not null)
            {
                Group(groups, "Hardware ohne eigene Firmware", input.Extras.Hardware.Where(h => h.Station == station).Select(h => DeviceNode(h.Device)), counted: true);
            }
            var adapter = members.Select(c => c.Own("adapter")).OfType<string>().FirstOrDefault();
            children.Add(new TreeNode($"Station {station}", adapter, groups));
        }

        var loose = new List<TreeNode>();
        Group(loose, "Nicht zugeordnete Geräte", sbom.Components.Where(c => c.Type == "firmware" && c.Station is null && c.InputSbom is null).Select(c => FirmwareNode(sbom, c)), counted: true);
        Group(loose, "Teil-SBOMs der Maschine", sbom.Components.Where(c => c.Station is null && c.Own("inputSbom:kind") == "partial").Select(c => PartialNode(sbom, c)));
        Group(loose, "Weitere Komponenten", sbom.Components.Where(c => c.Station is null && c.InputSbom is null && c.Type != "firmware" && c.Parent is null).Select(Node), counted: true);
        if (input.Extras is not null)
        {
            Group(loose, "Nicht zugeordnete Hardware ohne Firmware", input.Extras.Hardware.Where(h => h.Station is null).Select(h => DeviceNode(h.Device)), counted: true);
            Group(loose, "Ausgeschlossene Geräte", input.Extras.Ignored.Select(h => DeviceNode(h.Device)), counted: true);
        }
        children.AddRange(loose);

        var machine = sbom.Primary;
        return new TreeNode(machine?.Name ?? "Maschine", machine?.Version, children);
    }

    private static void Group(List<TreeNode> target, string label, IEnumerable<TreeNode> nodes, bool counted = false)
    {
        var list = nodes.ToList();
        if (list.Count > 0)
        {
            target.Add(new TreeNode(counted ? $"{label} ({list.Count})" : label, null, list));
        }
    }

    private static TreeNode Node(SbomComponent c) =>
        new(c.Name, c.Version ?? (c.Own("version:requested") is { } requested ? $"angefragt {requested}" : "ohne Version"));

    private static TreeNode FirmwareNode(SbomView sbom, SbomComponent c)
    {
        var detail = string.Join(" · ", new[] { c.Version ?? "Firmware unbekannt", c.Own("device:orderNumber"), c.Own("device:position") }.OfType<string>());
        var parts = (sbom.DependsOn.GetValueOrDefault(c.BomRef ?? "") ?? [])
            .Select(sbom.Find)
            .OfType<SbomComponent>()
            .Select(p => new TreeNode(p.Name, (p.Version ?? "ohne Version") + " (Hersteller-SBOM)"))
            .ToList();
        return new TreeNode(c.Name, detail, parts);
    }

    private static TreeNode PartialNode(SbomView sbom, SbomComponent root)
    {
        // Each component once, at its first place; dependencies of a partial SBOM may form cycles.
        var seen = new HashSet<string>(StringComparer.Ordinal) { root.BomRef ?? "" };
        List<TreeNode> Parts(SbomComponent c) =>
            (sbom.DependsOn.GetValueOrDefault(c.BomRef ?? "") ?? [])
                .Where(seen.Add)
                .Select(sbom.Find)
                .OfType<SbomComponent>()
                .ToList()
                .Select(p => Node(p) with { Children = Parts(p) })
                .ToList();
        return new TreeNode(root.Name, $"{root.Version ?? "ohne Version"} · {root.InputSbom}", Parts(root));
    }

    private static TreeNode DeviceNode(Device d) =>
        new(d.TypeName, string.Join(" · ", new[] { d.OrderNumber, d.Position ?? d.DeviceName }.OfType<string>()) is { Length: > 0 } s ? s : null);

    private static Table ComponentTable(IEnumerable<SbomComponent> components, Dictionary<string, List<Finding>> findings, Dictionary<string, string> gapCodes)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var c in components)
        {
            var own = c.BomRef is { } r ? findings.GetValueOrDefault(r) ?? [] : [];
            rows.Add(
            [
                Kind(c),
                c.Name,
                c.Version ?? (c.Own("version:requested") is { } requested ? $"– (angefragt {requested})" : Dash),
                c.Cpe ?? c.Purl ?? c.BomRef ?? Dash,
                Status(own, gapCodes),
                Origin(c),
            ]);
        }
        return new Table(["Typ", "Name", "Version", "Kennung", "Pflichtfelder", "Herkunft"], rows);
    }

    /// <summary>"vollständig" or the codes per severity, e.g. "Warnung: license-unknown; Hinweis: hash-unavailable".</summary>
    private static string Status(List<Finding> findings, Dictionary<string, string> gapCodes)
    {
        if (findings.Count == 0)
        {
            return "vollständig";
        }
        return string.Join("; ", findings
            .GroupBy(f => f.Severity)
            .OrderByDescending(g => g.Key)
            .Select(g => $"{SeverityText(g.Key)}: {string.Join(", ", g.Select(f => gapCodes.GetValueOrDefault(f.Code) ?? f.Code).Distinct(StringComparer.Ordinal))}"));
    }

    private static string Kind(SbomComponent c)
    {
        if (c.Own("inputSbom:kind") == "partial")
        {
            return "Teil-SBOM";
        }
        return c.SoftwareKind switch
        {
            "user-program" => "Anwenderprogramm",
            "runtime" => "Laufzeit",
            "library" => "Bibliothek",
            "library-type" => "Bibliothekstyp",
            "add-on-instruction" => "Add-On Instruction",
            "protected-block" => "Geschützter Baustein",
            _ => c.Type switch
            {
                "firmware" => "Firmware",
                "application" => "Anwendung",
                "library" => "Bibliothek",
                "framework" => "Framework",
                "operating-system" => "Betriebssystem",
                "file" => "Datei",
                "container" => "Container",
                "device" => "Gerät",
                _ => c.Type.Length == 0 ? Dash : c.Type,
            },
        };
    }

    /// <summary>Origin of vendor and values as written by the adapters (source, gsdml, devdesc, declared, assumed).</summary>
    private static string Origin(SbomComponent c)
    {
        var parts = new List<string>();
        if ((c.Own("device:vendor:origin") ?? c.Own("vendor:origin")) is { } vendor)
        {
            parts.Add($"Hersteller {vendor}");
        }
        if (c.Own("firmware:origin") is { } firmware)
        {
            parts.Add($"Firmware {firmware}");
        }
        if (c.Own("origin") is { } origin)
        {
            parts.Add($"Angabe {origin}");
        }
        if (c.InputSbom is { } inputSbom && c.Type != "firmware")
        {
            parts.Add($"aus {inputSbom}");
        }
        return parts.Count == 0 ? Dash : string.Join(", ", parts);
    }

    private static Table HardwareTable(IEnumerable<HardwareItem> items, bool withReason)
    {
        var rows = items.Select(h => (IReadOnlyList<string>)
        [
            h.Station ?? Dash,
            h.Device.DeviceName ?? Dash,
            h.Device.TypeName,
            h.Device.OrderNumber ?? Dash,
            h.Device.Position ?? Dash,
            h.Device.Vendor?.Name ?? Dash,
            .. withReason
                ? [h.Device.SourceProperties.GetValueOrDefault("device:passive") == "declared" ? "als passiv erklärt (passiveOrderNumbers)" : "laut Quelle ohne eigene Firmware"]
                : Array.Empty<string>(),
        ]).ToList();
        string[] header = withReason
            ? ["Station", "Gerät", "Typ", "Bestellnummer", "Position", "Hersteller", "Grund"]
            : ["Station", "Gerät", "Typ", "Bestellnummer", "Position", "Hersteller"];
        return new Table(header, rows);
    }

    private static void AddComparison(List<Block> blocks, Comparison comparison)
    {
        var field = comparison.Field;
        blocks.Add(new Heading(2, "Abgleich mit dem Feld"));
        blocks.Add(new Facts(
        [
            new("Feld-Datei", field.SourcePath),
            new("SHA-256", field.Sha256),
            new("Seriennummer der Maschine", field.SerialNumber),
            new("Ausgelesen am", field.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
            new("Anlass", field.Source ?? Dash),
            new("Geprüft", $"{comparison.Checked} Geräte der SBOM"),
            new("Ergebnis", string.Join(", ",
                $"{comparison.Count(ComparisonStatus.Equal)} gleich",
                $"{comparison.Count(ComparisonStatus.Different)} abweichend",
                $"{comparison.Count(ComparisonStatus.ProjectOnly)} nur im Projekt",
                $"{comparison.Count(ComparisonStatus.FieldOnly)} nur im Feld",
                $"{comparison.Count(ComparisonStatus.Passive)} passiv")),
        ]));
        blocks.Add(new Table(
            ["Station", "Position", "Gerät", "Bestellnummer SBOM", "Bestellnummer Feld", "Firmware SBOM", "Firmware Feld", "Ergebnis", "Hinweis"],
            comparison.Entries.Select(e => (IReadOnlyList<string>)
            [
                e.Station ?? Dash,
                e.Position ?? Dash,
                e.Device,
                e.SbomOrderNumber ?? Dash,
                e.Field?.OrderNumber ?? Dash,
                e.SbomFirmware ?? Dash,
                e.Field?.Firmware ?? Dash,
                e.StatusText,
                e.Notes.Count == 0 ? Dash : string.Join(" ", e.Notes),
            ]).ToList()));
        foreach (var warning in field.Warnings)
        {
            blocks.Add(new Paragraph($"[{warning.Code}] {warning.Message}"));
        }
    }

    private static string Party(SbomParty? party) =>
        party is null ? Dash : (party.Email ?? party.Url) is { } contact ? $"{party.Name} ({contact})" : party.Name;

    private static string Tool(string name, string version, string? release) =>
        $"{name} {version}{(release is null ? "" : $" ({release})")}".Trim();

    private static string SeverityText(Severity severity) => severity switch
    {
        Severity.Error => "Fehler",
        Severity.Warning => "Warnung",
        _ => "Hinweis",
    };

    private static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
}
