using System.Globalization;
using System.Xml.Linq;

namespace PlcSbom.Core.Adapters.Studio5000;

// Raw inventory of a Studio 5000 L5X export (CLAUDE.md section 5.2, docs/adapter-studio5000.md).
// Values stay as written in the file; interpretation happens in Studio5000Adapter.
// Port addresses are read only for backplane ports (slot number). Ethernet ports carry IP addresses,
// which never leave the reader (CLAUDE.md section 2, network data).

/// <summary>Whole export: header, controller, modules and Add-On Instructions.</summary>
public sealed record L5xProject(
    string? SchemaRevision,
    string? SoftwareRevision,
    string? TargetName,
    string? TargetType,
    bool ContainsContext,
    string? ExportDate,
    L5xController? Controller,
    IReadOnlyList<L5xModule> Modules,
    IReadOnlyList<L5xAddOnInstruction> AddOnInstructions);

/// <summary>The Controller element. Use is "Target" in a controller export, "Context" in a component export.</summary>
public sealed record L5xController(
    string Name,
    string? Use,
    string? ProcessorType,
    string? MajorRev,
    string? MinorRev,
    bool? RedundancyEnabled);

/// <summary>One Module element. <see cref="Slot"/> only from a backplane port (Type ICP).</summary>
public sealed record L5xModule(
    string Name,
    string? CatalogNumber,
    string? Vendor,
    string? ProductType,
    string? ProductCode,
    string? Major,
    string? Minor,
    string? ParentModule,
    string? ParentModPortId,
    string? Ekey,
    string? UpstreamPortType,
    int? Slot);

/// <summary>One AddOnInstructionDefinition element.</summary>
public sealed record L5xAddOnInstruction(
    string Name,
    string? Use,
    string? Revision,
    string? RevisionExtension,
    string? Vendor);

/// <summary>Reads a Studio 5000 L5X export.</summary>
public static class L5xReader
{
    public const string RootElement = "RSLogix5000Content";

    /// <summary>Backplane port type of ControlLogix chassis; its Address is the slot number.</summary>
    public const string BackplanePortType = "ICP";

    public static L5xProject Read(byte[] content, string displayPath)
    {
        var root = SafeXml.Load(content, displayPath).Root!;
        if (root.Name.LocalName != RootElement)
        {
            throw new UsageException(
                "NOT_L5X",
                $"Die Datei {displayPath} ist kein L5X-Export aus Studio 5000 (Wurzelelement „{root.Name.LocalName}“ statt „{RootElement}“).");
        }

        var controller = root.ElementNamed("Controller");
        var modules = controller?.ElementNamed("Modules")?.ElementsNamed("Module").Select(ReadModule).ToList() ?? [];
        var aois = controller?.ElementNamed("AddOnInstructionDefinitions")?.ElementsNamed("AddOnInstructionDefinition")
            .Select(a => new L5xAddOnInstruction(
                a.AttributeValue("Name") ?? "",
                a.AttributeValue("Use"),
                a.AttributeValue("Revision")?.Trim(),
                a.AttributeValue("RevisionExtension"),
                a.AttributeValue("Vendor")))
            .ToList() ?? [];

        return new L5xProject(
            root.AttributeValue("SchemaRevision"),
            root.AttributeValue("SoftwareRevision"),
            root.AttributeValue("TargetName"),
            root.AttributeValue("TargetType"),
            string.Equals(root.AttributeValue("ContainsContext"), "true", StringComparison.OrdinalIgnoreCase),
            root.AttributeValue("ExportDate"),
            controller is null ? null : new L5xController(
                controller.AttributeValue("Name") ?? "",
                controller.AttributeValue("Use"),
                controller.AttributeValue("ProcessorType"),
                controller.AttributeValue("MajorRev"),
                controller.AttributeValue("MinorRev"),
                controller.ElementNamed("RedundancyInfo")?.AttributeValue("Enabled") is { } redundancy
                    ? string.Equals(redundancy, "true", StringComparison.OrdinalIgnoreCase)
                    : null),
            modules,
            aois);
    }

    private static L5xModule ReadModule(XElement module)
    {
        var name = module.AttributeValue("Name") ?? "";
        var parent = module.AttributeValue("ParentModule");
        var parentPort = module.AttributeValue("ParentModPortId");
        var ports = module.ElementNamed("Ports")?.ElementsNamed("Port").ToList() ?? [];

        // The port that connects the module to its parent is the upstream one. The controller is its own
        // parent and has no upstream port; its port with the id it names as ParentModPortId sits on the backplane.
        var upstream = parent == name
            ? ports.FirstOrDefault(p => p.AttributeValue("Id") == parentPort)
            : ports.FirstOrDefault(p => string.Equals(p.AttributeValue("Upstream"), "true", StringComparison.OrdinalIgnoreCase));
        var upstreamType = upstream?.AttributeValue("Type");
        int? slot = upstreamType == BackplanePortType
            && int.TryParse(upstream!.AttributeValue("Address"), NumberStyles.None, CultureInfo.InvariantCulture, out var s)
            ? s
            : null;

        return new L5xModule(
            name,
            module.AttributeValue("CatalogNumber"),
            module.AttributeValue("Vendor"),
            module.AttributeValue("ProductType"),
            module.AttributeValue("ProductCode"),
            module.AttributeValue("Major"),
            module.AttributeValue("Minor"),
            parent,
            parentPort,
            module.ElementNamed("EKey")?.AttributeValue("State"),
            upstreamType,
            slot);
    }
}
