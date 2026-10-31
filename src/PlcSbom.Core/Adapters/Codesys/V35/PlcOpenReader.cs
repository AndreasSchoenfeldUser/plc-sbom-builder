using System.Xml.Linq;

namespace PlcSbom.Core.Adapters.Codesys.V35;

// Raw inventory of a CODESYS V3.5 PLCopen XML export (CLAUDE.md section 5.1.1, docs/adapter-codesys.md).
// Values stay as written in the file; interpretation happens in CodesysV35Adapter.

/// <summary>Whole export: header, devices with their applications, libraries of the POU pool.</summary>
public sealed record PlcOpenProject(
    string? ProductName,
    string? ProductVersion,
    string? CompanyName,
    string? CreationDateTime,
    string? ProjectName,
    string? ProjectVersion,
    IReadOnlyList<PlcOpenDevice> Devices,
    IReadOnlyList<PlcOpenLibrary> ProjectLibraries);

/// <summary>A configuration with CODESYS device data. Top-level devices are controllers.</summary>
public sealed record PlcOpenDevice(
    string Name,
    string? Type,
    string? Id,
    string? Version,
    IReadOnlyList<PlcOpenApplication> Applications,
    IReadOnlyList<PlcOpenDevice> Children);

/// <summary>A PLCopen resource, in CODESYS an application with its library manager.</summary>
public sealed record PlcOpenApplication(string Name, IReadOnlyList<PlcOpenLibrary> Libraries);

public enum LibraryResolution
{
    /// <summary>Direct reference with title, version and company in Library@Name.</summary>
    Fixed,

    /// <summary>Placeholder redirected in the same library manager (PlaceholderRedirection).</summary>
    Redirection,

    /// <summary>Placeholder without redirection: only DefaultResolution is known.</summary>
    Default,

    /// <summary>Placeholder without redirection and without DefaultResolution.</summary>
    None,
}

/// <summary>One Library element with the resolution the export shows.</summary>
public sealed record PlcOpenLibrary(
    string Reference,
    string? Namespace,
    string? Placeholder,
    string? DefaultResolution,
    string? Redirection,
    bool SystemLibrary,
    LibraryResolution Resolution,
    string? Effective,
    LibraryReference? Parsed);

/// <summary>Reads a PLCopen XML export of CODESYS V3.5.</summary>
public static class PlcOpenReader
{
    public const string PlcOpenNamespacePrefix = "http://www.plcopen.org/xml/tc6";
    private const string LibrariesData = "http://www.3s-software.com/plcopenxml/libraries";
    private const string DeviceData = "Device";
    private const string ConfigurationsData = "configurations";

    public static PlcOpenProject Read(byte[] content, string displayPath)
    {
        var root = SafeXml.Load(content, displayPath).Root!;
        if (root.Name.LocalName != "project" || !root.Name.NamespaceName.StartsWith(PlcOpenNamespacePrefix, StringComparison.Ordinal))
        {
            throw new UsageException(
                "NOT_PLCOPEN",
                $"Die Datei {displayPath} ist kein PLCopen-XML-Export (Wurzelelement „{root.Name.LocalName}“, Namespace „{root.Name.NamespaceName}“). In CODESYS V3.5 das ganze Projekt als PLCopen XML exportieren.");
        }

        var header = root.ElementNamed("fileHeader");
        var contentHeader = root.ElementNamed("contentHeader");
        var configurations = root.ElementNamed("instances")?.ElementNamed("configurations");
        var devices = configurations is null ? [] : Devices(configurations.ElementsNamed("configuration"));

        return new PlcOpenProject(
            header?.AttributeValue("productName"),
            header?.AttributeValue("productVersion"),
            header?.AttributeValue("companyName"),
            header?.AttributeValue("creationDateTime"),
            contentHeader?.AttributeValue("name"),
            contentHeader?.AttributeValue("version"),
            devices,
            Libraries(root));
    }

    /// <summary>
    /// Configurations with device data become devices. Configurations without (e.g. "SoftMotion General
    /// Axis Pool" in robin) are transparent: their device children move up one level.
    /// </summary>
    private static List<PlcOpenDevice> Devices(IEnumerable<XElement> configurations)
    {
        var result = new List<PlcOpenDevice>();
        foreach (var configuration in configurations)
        {
            var nested = Data(configuration, ConfigurationsData)?.ElementNamed("configurations")?.ElementsNamed("configuration") ?? [];
            var children = Devices(nested);
            var identification = Data(configuration, DeviceData)?.ElementNamed("Device")?.ElementNamed("DeviceType")?.ElementNamed("DeviceIdentification");
            if (identification is null)
            {
                result.AddRange(children);
                continue;
            }
            var applications = configuration.ElementsNamed("resource")
                .Select(r => new PlcOpenApplication(r.AttributeValue("name") ?? "", Libraries(r)))
                .ToList();
            result.Add(new PlcOpenDevice(
                configuration.AttributeValue("name") ?? "",
                identification.ChildText("Type"),
                identification.ChildText("Id"),
                identification.ChildText("Version"),
                applications,
                children));
        }
        return result;
    }

    /// <summary>Library elements in the libraries data directly under the element's addData.</summary>
    private static List<PlcOpenLibrary> Libraries(XElement owner)
    {
        var libraries = Data(owner, LibrariesData)?.ElementNamed("Libraries");
        if (libraries is null)
        {
            return [];
        }
        // Redirections apply to the library manager they stand in (CODESYS help "Placeholder":
        // "always possible to overwrite the resolution of a placeholder in the application context").
        var redirections = (libraries.ElementNamed("PlaceholderRedirections")?.ElementsNamed("PlaceholderRedirection") ?? [])
            .Select(r => (Placeholder: r.AttributeValue("Placeholder"), Redirection: r.AttributeValue("Redirection")))
            .Where(r => r.Placeholder is not null && r.Redirection is not null)
            .GroupBy(r => r.Placeholder!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Redirection!, StringComparer.Ordinal);

        var result = new List<PlcOpenLibrary>();
        foreach (var library in libraries.ElementsNamed("Library"))
        {
            var reference = library.AttributeValue("Name") ?? "";
            var placeholder = reference.StartsWith('#') ? reference[1..] : null;
            var defaultResolution = library.AttributeValue("DefaultResolution");
            var redirection = placeholder is null ? null : redirections.GetValueOrDefault(placeholder);
            var (resolution, effective) =
                placeholder is null ? (LibraryResolution.Fixed, reference)
                : redirection is not null ? (LibraryResolution.Redirection, redirection)
                : defaultResolution is not null ? (LibraryResolution.Default, defaultResolution)
                : (LibraryResolution.None, (string?)null);
            result.Add(new PlcOpenLibrary(
                reference,
                library.AttributeValue("Namespace"),
                placeholder,
                defaultResolution,
                redirection,
                string.Equals(library.AttributeValue("SystemLibrary"), "true", StringComparison.OrdinalIgnoreCase),
                resolution,
                effective,
                LibraryReference.Parse(effective)));
        }
        return result;
    }

    private static XElement? Data(XElement owner, string name) =>
        owner.ElementNamed("addData")?.ElementsNamed("data").FirstOrDefault(d => d.AttributeValue("name") == name);
}
