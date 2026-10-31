namespace PlcSbom.Core.Model;

// Internal model (CLAUDE.md section 4.2). Adapters fill it, the writers turn it into an SBOM format.
// Nothing here knows CycloneDX. A null value means "the source does not provide it"; the gap rules
// (GapRules) and the validator decide what that means.

/// <summary>The product placed on the market. Primary component of the SBOM.</summary>
public sealed record Machine
{
    public required string Name { get; init; }

    /// <summary>Release identifier, e.g. R2026.10. Version of the primary component.</summary>
    public required string Release { get; init; }

    /// <summary>Full Git commit the release was built from.</summary>
    public string? Commit { get; init; }

    /// <summary>Serial number of one machine (only for as-built SBOMs).</summary>
    public string? SerialNumber { get; init; }

    /// <summary>Machine builder: creator of the machine and of this SBOM (TR 5.2.1, 5.2.2).</summary>
    public required Party Supplier { get; init; }

    /// <summary>Licence of the machine software as declared by the machine builder.</summary>
    public Licensing? Licensing { get; init; }

    public IReadOnlyList<Station> Stations { get; init; } = [];

    /// <summary>Devices that belong to no station (gap device-unassigned).</summary>
    public IReadOnlyList<Device> UnassignedDevices { get; init; } = [];

    public IReadOnlyList<Gap> Gaps { get; init; } = [];
}

/// <summary>A controller with everything attached to it. Read by exactly one adapter.</summary>
public sealed record Station
{
    public required string Name { get; init; }

    /// <summary>Adapter id, see <see cref="AdapterIds"/>.</summary>
    public required string AdapterId { get; init; }

    /// <summary>Source variant within the adapter, e.g. v35, v23, c4 for codesys. Written as &lt;ns&gt;:adapter:source.</summary>
    public string? AdapterSource { get; init; }

    /// <summary>Source file relative to the repository, with '/'.</summary>
    public required string SourcePath { get; init; }

    public string? SourceSha256 { get; init; }

    /// <summary>Export time stated in the source (AML LastWritingDateTime).</summary>
    public DateTimeOffset? SourceExportedAt { get; init; }

    public ToolInfo? EngineeringTool { get; init; }

    public IReadOnlyList<Device> Devices { get; init; } = [];

    public IReadOnlyList<SoftwareItem> Software { get; init; } = [];

    public IReadOnlyList<Gap> Gaps { get; init; } = [];
}

/// <summary>Engineering software a source was written with, e.g. TIA Portal V21 (2100.0.121.1).</summary>
public sealed record ToolInfo(string Name, string Version, string? Release = null, string? Vendor = null);

/// <summary>Organisation with its contact. TR 5.2.1/5.2.2 need an email address, otherwise a URL.</summary>
public sealed record Party(string Name, string? Email = null, string? Url = null)
{
    public bool HasContact => !string.IsNullOrWhiteSpace(Email) || !string.IsNullOrWhiteSpace(Url);
}

/// <summary>Hardware with an order number. Its firmware becomes a component, the device itself does not (ADR E-007).</summary>
public sealed record Device
{
    /// <summary>Type name from the source, e.g. CPU 1511-1 PN.</summary>
    public required string TypeName { get; init; }

    public Party? Vendor { get; init; }

    public ValueOrigin VendorOrigin { get; init; } = ValueOrigin.Source;

    /// <summary>Order or catalogue number, unchanged from the source (with spaces).</summary>
    public string? OrderNumber { get; init; }

    /// <summary>The source contains placeholders such as 6ES7 590-1***0-0AA0.</summary>
    public bool OrderNumberIsPattern { get; init; }

    /// <summary>Name in the project, e.g. PLC_1.</summary>
    public string? DeviceName { get; init; }

    /// <summary>Path from station, rack and slot, e.g. 0/1.</summary>
    public string? Position { get; init; }

    /// <summary>Classification as stated by the source (CPU, HeadModule …).</summary>
    public string? Classification { get; init; }

    /// <summary>The controller of the station; the user program runs on its firmware.</summary>
    public bool IsController { get; init; }

    /// <summary>Firmware version as written in the source, e.g. V2.8 or 4.7.10.</summary>
    public string? Firmware { get; init; }

    public FirmwareOrigin FirmwareOrigin { get; init; } = FirmwareOrigin.Unknown;

    /// <summary>No own firmware: goes to the hardware appendix of the report, not into the SBOM.</summary>
    public bool IsPassive { get; init; }

    /// <summary>CPE of the firmware, only from the user's CPE table (CLAUDE.md section 6), never derived.</summary>
    public string? Cpe { get; init; }

    /// <summary>Licence of the firmware, e.g. from machine.yml "licenses".</summary>
    public Licensing? FirmwareLicensing { get; init; }

    /// <summary>Vendor keys such as GSDML, VendorId, ProductCode, RevisionNo.</summary>
    public IReadOnlyDictionary<string, string> VendorKeys { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<Gap> Gaps { get; init; } = [];
}

public enum FirmwareOrigin
{
    /// <summary>Configured value from the engineering project.</summary>
    Projected,

    /// <summary>Read from the device in the field.</summary>
    AsFound,

    /// <summary>The project does not carry a firmware version for this device.</summary>
    NotInProject,

    /// <summary>The source states a version that is not the firmware (e.g. device description).</summary>
    Unknown,
}

public enum ValueOrigin
{
    /// <summary>Read from the engineering source.</summary>
    Source,

    /// <summary>Looked up in a GSDML file.</summary>
    Gsdml,

    /// <summary>Looked up in a CODESYS device description (*.devdesc.xml).</summary>
    DeviceDescription,

    /// <summary>Declared by the user in machine.yml.</summary>
    Declared,

    /// <summary>Documented assumption, e.g. vendor of OrderNumber: devices in TIA.</summary>
    Assumed,
}

/// <summary>Software of a station other than firmware.</summary>
public sealed record SoftwareItem
{
    public required SoftwareKind Kind { get; init; }

    public required string Name { get; init; }

    public Party? Vendor { get; init; }

    public ValueOrigin VendorOrigin { get; init; } = ValueOrigin.Source;

    public string? Version { get; init; }

    /// <summary>False for placeholders such as "*" or "latest installed".</summary>
    public bool VersionResolved { get; init; } = true;

    /// <summary>Library of a library type, e.g. LGF.</summary>
    public string? Library { get; init; }

    public ValueOrigin Origin { get; init; } = ValueOrigin.Source;

    /// <summary>File name of the deployable form (not a path), if one exists.</summary>
    public string? FileName { get; init; }

    /// <summary>BSI properties executable/archive/structured. Null: not known, gap file-properties-unknown.</summary>
    public FileTraits? Traits { get; init; }

    public Licensing? Licensing { get; init; }

    public string? Cpe { get; init; }

    /// <summary>Package URL, only for real software packages (e.g. from partial SBOMs).</summary>
    public string? Purl { get; init; }

    /// <summary>URI of the deployable form (TR 5.2.4). Required to write <see cref="DeployableSha512"/>.</summary>
    public string? DeployableUri { get; init; }

    /// <summary>SHA-512 of the deployable form (TR 5.2.2).</summary>
    public string? DeployableSha512 { get; init; }

    /// <summary>URI of the source code (TR 5.2.4). Required to write <see cref="SourceSha512"/>.</summary>
    public string? SourceUri { get; init; }

    /// <summary>SHA-512 over the source code (TR 5.2.5, optional).</summary>
    public string? SourceSha512 { get; init; }

    /// <summary>Further facts from the source, written as &lt;ns&gt;:&lt;key&gt;, e.g. library:placeholder.</summary>
    public IReadOnlyDictionary<string, string> SourceProperties { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<Gap> Gaps { get; init; } = [];
}

public enum SoftwareKind
{
    UserProgram,
    Library,
    LibraryType,
    ProtectedBlock,
    Runtime,
    AddOnInstruction,
}

/// <summary>TR 5.2.2 executable, archive and structured property of a component.</summary>
public sealed record FileTraits(bool Executable, bool Archive, bool Structured)
{
    /// <summary>Firmware image: executable, unstructured archive (TR 8.1.6, 8.1.7 table 7).</summary>
    public static FileTraits Firmware { get; } = new(Executable: true, Archive: true, Structured: false);
}

/// <summary>Licence information in the terms of TR 3.2.8, each an SPDX identifier or expression.</summary>
public sealed record Licensing(string? Distribution, string? Original = null, string? Effective = null);

/// <summary>A required statement no source provides. Recorded, never filled (CLAUDE.md section 4.4).</summary>
public sealed record Gap(string Code, string Message);
