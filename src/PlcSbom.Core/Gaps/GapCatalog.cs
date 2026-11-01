namespace PlcSbom.Core.Gaps;

/// <summary>Severity of a finding (CLAUDE.md section 4.4).</summary>
public enum Severity
{
    /// <summary>Gap the TR allows or that only the as-found state closes. Never changes the exit code.</summary>
    Note,

    /// <summary>Required statement missing, gap recorded. Exit 1 only with --release-build.</summary>
    Warning,

    /// <summary>Schema violated or required field missing without gap. Always exit 1.</summary>
    Error,
}

/// <summary>TR 03183-2 data fields as checked per component and per SBOM. Stable identifiers (result file).</summary>
public static class TrField
{
    public const string SbomFormat = "sbom.format";
    public const string SbomCreator = "sbom.creator";
    public const string SbomTimestamp = "sbom.timestamp";
    public const string SbomVulnerabilities = "sbom.vulnerabilities";
    public const string SbomPrimaryComponent = "sbom.primaryComponent";
    public const string Creator = "creator";
    public const string Name = "name";
    public const string Version = "version";
    public const string Filename = "filename";
    public const string Dependencies = "dependencies";
    public const string DependencyCompleteness = "dependencyCompleteness";

    /// <summary>Every component lies on a path from the primary component (TR 5.1).</summary>
    public const string DependencyGraph = "dependencyGraph";
    public const string DistributionLicence = "distributionLicence";
    public const string Hash = "hash";
    public const string Executable = "executable";
    public const string Archive = "archive";
    public const string Structured = "structured";
    public const string EffectiveLicence = "effectiveLicence";
    public const string BomRef = "bom-ref";
}

/// <summary>A gap code with its severity and the required TR fields it accounts for.</summary>
public sealed record GapDefinition(string Code, Severity Severity, IReadOnlyList<string> Excuses, string Description);

/// <summary>
/// Gap codes (CLAUDE.md section 4.4, extended; reasons per code in docs/regeln-tr03183.md).
/// A missing required field is a warning or note if a gap that excuses it is recorded, otherwise an error.
/// </summary>
public static class GapCatalog
{
    public const string FirmwareNotInProject = "firmware-not-in-project";
    public const string FirmwareAttributeMissing = "firmware-attribute-missing";
    public const string FirmwareProjectedOnly = "firmware-projected-only";
    public const string HashUnavailable = "hash-unavailable";
    public const string FilenameUnavailable = "filename-unavailable";
    public const string VersionUnresolved = "version-unresolved";
    public const string LibrariesNotRead = "libraries-not-read";
    public const string RuntimeVersionUnknown = "runtime-version-unknown";
    public const string EkeyDisabled = "ekey-disabled";
    public const string LicenseUnknown = "license-unknown";
    public const string VendorSbomMissing = "vendor-sbom-missing";
    public const string ProtectedBlockUnknown = "protected-block-unknown";
    public const string GsdmlNotFound = "gsdml-not-found";
    public const string DevdescNotFound = "devdesc-not-found";
    public const string OrderNumberNotInProject = "order-number-not-in-project";
    public const string SupplierUnknown = "supplier-unknown";
    public const string CreatorContactUnknown = "creator-contact-unknown";
    public const string DeviceUnassigned = "device-unassigned";
    public const string DevicesNotRead = "devices-not-read";
    public const string ProgramNotRead = "program-not-read";
    public const string ProgramSourceMissing = "program-source-missing";
    public const string FilePropertiesUnknown = "file-properties-unknown";
    public const string InputFieldMissing = "input-field-missing";

    private static readonly GapDefinition[] Definitions =
    [
        new(FirmwareNotInProject, Severity.Note, [TrField.Version], "Das Projekt führt keine Firmware-Version für dieses Gerät."),
        new(FirmwareAttributeMissing, Severity.Warning, [], "Die Quelle führt für dieses Gerät kein Firmware-Attribut; ob es eine eigene Firmware hat, ist offen."),
        new(FirmwareProjectedOnly, Severity.Note, [], "Die Firmware ist nur als projektierter Sollwert bekannt, ein Ist-Stand liegt nicht vor."),
        new(HashUnavailable, Severity.Note, [TrField.Hash], "Die ausgelieferte Form liegt dem Werkzeug nicht vor, ein SHA-512 ist nicht bestimmbar."),
        new(FilenameUnavailable, Severity.Note, [TrField.Filename], "Die Komponente liegt nicht als Datei vor, ein Dateiname ist nicht bestimmbar."),
        new(VersionUnresolved, Severity.Warning, [TrField.Version], "Die Quelle nennt keine feste Version, sondern einen Platzhalter."),
        new(LibrariesNotRead, Severity.Warning, [], "Die Quelle enthält keine Bibliotheken, und keine sind deklariert."),
        new(RuntimeVersionUnknown, Severity.Warning, [TrField.Version], "Die Version der Laufzeit steht nicht im Projekt."),
        new(EkeyDisabled, Severity.Warning, [], "Die elektronische Kodierung ist aus, das Gerät im Feld kann beliebig abweichen."),
        new(LicenseUnknown, Severity.Warning, [TrField.DistributionLicence], "Keine Distributionslizenz bekannt."),
        new(VendorSbomMissing, Severity.Note, [], "Für diese Firmware liegt keine SBOM des Herstellers vor."),
        new(ProtectedBlockUnknown, Severity.Warning, [TrField.Creator, TrField.Version], "Geschützter Baustein ohne bekannten Lieferanten oder Version."),
        new(GsdmlNotFound, Severity.Warning, [], "Die GSDML-Datei des Geräts liegt in keinem der Verzeichnisse aus „gsdml“, liegt dort mehrfach mit verschiedenem Inhalt, oder es ist kein Verzeichnis angegeben."),
        new(DevdescNotFound, Severity.Warning, [], "Zur Kennung des Geräts liegt keine eindeutige CODESYS-Gerätebeschreibung in den Verzeichnissen aus „deviceDescriptions“."),
        new(OrderNumberNotInProject, Severity.Warning, [], "Weder Projekt noch GSDML nennen eine Bestellnummer."),
        new(SupplierUnknown, Severity.Warning, [TrField.Creator], "Der Hersteller ist nicht ermittelbar."),
        new(CreatorContactUnknown, Severity.Warning, [TrField.Creator], "Für den Hersteller ist weder E-Mail-Adresse noch URL bekannt."),
        new(DeviceUnassigned, Severity.Note, [], "Das Gerät gehört zu keiner Station."),
        new(DevicesNotRead, Severity.Warning, [], "Die Quelle enthält Geräte, die der Adapter nicht ausliest; sie fehlen in der SBOM."),
        new(ProgramNotRead, Severity.Note, [], "Die Quelle enthält keine Bausteine, geschützte Bausteine sind unbekannt."),
        new(ProgramSourceMissing, Severity.Warning, [TrField.Hash], "Für das Anwenderprogramm ist kein Quellverzeichnis angegeben, es gibt keinen Hash."),
        new(FilePropertiesUnknown, Severity.Warning, [TrField.Executable, TrField.Archive, TrField.Structured], "Ob die Komponente ausführbar, ein Archiv oder strukturiert ist, ist nicht bekannt."),
        new(InputFieldMissing, Severity.Warning,
            [TrField.Creator, TrField.Version, TrField.Filename, TrField.DistributionLicence, TrField.Hash, TrField.Executable, TrField.Archive, TrField.Structured],
            "Die Komponente stammt aus einer eingelesenen SBOM, der Pflichtfelder fehlen; sie wurde unverändert übernommen."),
    ];

    private static readonly Dictionary<string, GapDefinition> ByCode = Definitions.ToDictionary(d => d.Code, StringComparer.Ordinal);

    /// <summary>What closes a gap: shown in the report next to each code (CLAUDE.md section 8.2).</summary>
    private static readonly Dictionary<string, string> Remedies = new(StringComparer.Ordinal)
    {
        [FirmwareNotInProject] = "Ist-Stand aus dem Feld einlesen (plc-sbom compare --as-built).",
        [FirmwareAttributeMissing] = "Firmware im Projekt nachtragen oder das Gerät mit „passiveOrderNumbers“ in machine.yml als passiv erklären.",
        [FirmwareProjectedOnly] = "Ist-Stand aus dem Feld einlesen (plc-sbom compare --as-built).",
        [HashUnavailable] = "Hersteller-SBOM mit SHA-512 der Firmware-Datei unter „vendorSboms“ einbinden; bei Firmware und Anwenderprogramm sonst zulässig (TR 3.2.1).",
        [FilenameUnavailable] = "Hersteller-SBOM mit Dateinamen unter „vendorSboms“ einbinden; sonst zulässig (TR 3.2.1).",
        [VersionUnresolved] = "Bibliothek im Projekt auf eine feste Version setzen.",
        [LibrariesNotRead] = "Bibliotheken der Station unter „libraries“ in machine.yml deklarieren.",
        [RuntimeVersionUnknown] = "Version der Laufzeit aus dem Feld einlesen oder deklarieren.",
        [EkeyDisabled] = "Elektronische Kodierung des Moduls im Projekt einschalten oder den Ist-Stand prüfen.",
        [LicenseUnknown] = "Distributionslizenz des Herstellers unter „licenses“ in machine.yml eintragen.",
        [VendorSbomMissing] = "SBOM vom Hersteller anfordern und unter „vendorSboms“ einbinden.",
        [ProtectedBlockUnknown] = "Lieferanten unter „protectedBlockVendors“ in machine.yml eintragen.",
        [GsdmlNotFound] = "GSDML-Datei in ein Verzeichnis aus „gsdml“ legen; doppelte Dateien mit anderem Inhalt entfernen.",
        [DevdescNotFound] = "Passende Gerätebeschreibung (*.devdesc.xml) in ein Verzeichnis aus „deviceDescriptions“ legen.",
        [OrderNumberNotInProject] = "Bestellnummer über eine Gerätebeschreibung oder den Ist-Stand ermitteln.",
        [SupplierUnknown] = "Hersteller mit „vendorOverrides“ in machine.yml erklären.",
        [CreatorContactUnknown] = "E-Mail-Adresse oder URL des Herstellers unter „vendorContacts“ in machine.yml eintragen.",
        [DeviceUnassigned] = "Gerät mit „devices“ einer Station zuordnen oder mit „ignoreDevices“ ausschließen.",
        [DevicesNotRead] = "Geräte, die der Adapter nicht liest, über eine Hersteller- oder Teil-SBOM ergänzen.",
        [ProgramNotRead] = "Erst der Adapter tia-portal liest Bausteine (M14).",
        [ProgramSourceMissing] = "Quellverzeichnis der Station unter „program“ in machine.yml angeben.",
        [FilePropertiesUnknown] = "Bibliothek als Datei mit bekannten Eigenschaften einbinden; die Quelle sagt nichts dazu.",
        [InputFieldMissing] = "Vollständige SBOM beim Lieferanten bzw. vom erzeugenden Werkzeug anfordern.",
    };

    public static IReadOnlyList<GapDefinition> All => Definitions;

    public static GapDefinition? Find(string code) => ByCode.GetValueOrDefault(code);

    /// <summary>What closes the gap; null for codes the tool does not know (foreign SBOMs).</summary>
    public static string? Remedy(string code) => Remedies.GetValueOrDefault(code);

    /// <summary>Gap codes that make the dependency list of a station's program incomplete.</summary>
    public static IReadOnlySet<string> MakeDependenciesIncomplete { get; } =
        new HashSet<string>(StringComparer.Ordinal) { LibrariesNotRead, ProgramNotRead };
}
