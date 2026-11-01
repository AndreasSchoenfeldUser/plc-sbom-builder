using PlcSbom.Core.Adapters.TiaAml;

namespace PlcSbom.Core.Config;

/// <summary>
/// machine.yml (CLAUDE.md section 7.4), as read and checked by <see cref="MachineConfigLoader"/>.
/// Paths are kept as written; they are relative to the directory of the file.
/// </summary>
public sealed record MachineConfig
{
    /// <summary>Absolute path of machine.yml.</summary>
    public required string FullPath { get; init; }

    /// <summary>Absolute directory of machine.yml; base of all relative paths in it.</summary>
    public string Directory => Path.GetDirectoryName(FullPath)!;

    public required MachineSection Machine { get; init; }

    /// <summary>Property namespace of the user, default plcsbom.</summary>
    public string Namespace { get; init; } = "plcsbom";

    /// <summary>Directories with GSDML files.</summary>
    public IReadOnlyList<string> Gsdml { get; init; } = [];

    /// <summary>Directories with CODESYS device descriptions (*.devdesc.xml).</summary>
    public IReadOnlyList<string> DeviceDescriptions { get; init; } = [];

    /// <summary>Patterns of devices without firmware, declared passive by the user.</summary>
    public IReadOnlyList<string> PassiveOrderNumbers { get; init; } = [];

    /// <summary>Pattern → vendor, in file order; the first match wins.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> VendorOverrides { get; init; } = [];

    /// <summary>Pattern on block author or family → supplier (adapter tia-portal, M14).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> ProtectedBlockVendors { get; init; } = [];

    /// <summary>Vendor name → email address or URL, the creator contact of TR 5.2.2.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> VendorContacts { get; init; } = [];

    /// <summary>Vendor name → distribution licence (SPDX expression or LicenseRef-…).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Licenses { get; init; } = [];

    /// <summary>URL of a program's source directory with the placeholders {commit} and {path}.</summary>
    public string? SourceUrl { get; init; }

    public required IReadOnlyList<StationConfig> Stations { get; init; }

    /// <summary>Glob patterns of vendor SBOMs (*.cdx.json).</summary>
    public IReadOnlyList<string> VendorSboms { get; init; } = [];

    public IReadOnlyList<PartialSbomConfig> PartialSboms { get; init; } = [];

    /// <summary>CPE table (CLAUDE.md section 6).</summary>
    public string? CpeMap { get; init; }
}

/// <summary>The machine: name, machine builder and their contact (email or URL).</summary>
public sealed record MachineSection(string Name, string Supplier, string? Contact);

/// <summary>One station: exactly one adapter reads it (CLAUDE.md section 4.1).</summary>
public sealed record StationConfig
{
    public required string Name { get; init; }

    public required string Adapter { get; init; }

    /// <summary>Source variant of the adapter codesys: v35, v23 or c4.</summary>
    public string? Source { get; init; }

    /// <summary>Project file or export, relative to machine.yml.</summary>
    public required string Path { get; init; }

    /// <summary>Controller in the source (codesys, tia-aml, tia-portal).</summary>
    public string? Plc { get; init; }

    /// <summary>Source directory of the user program, for the program hash (CLAUDE.md section 5.6).</summary>
    public string? Program { get; init; }

    public IReadOnlyList<string> Devices { get; init; } = [];

    public IReadOnlyList<string> IgnoreDevices { get; init; } = [];

    public IReadOnlyList<DeclaredLibrary> Libraries { get; init; } = [];

    public bool AllowUpgrade { get; init; }

    public IReadOnlyList<string> GlobalLibraries { get; init; } = [];
}

/// <summary>A partial SBOM (Syft, cdxgen) and the station it belongs to; without station the machine.</summary>
public sealed record PartialSbomConfig(string Path, string? Station);
