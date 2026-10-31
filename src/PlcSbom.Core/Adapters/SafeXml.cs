using System.Xml;
using System.Xml.Linq;

namespace PlcSbom.Core.Adapters;

/// <summary>
/// Reads foreign XML files safely (CLAUDE.md section 2): no DTD, no resolver, so no external entities
/// and no entity expansion. Helpers work on local names, because the engineering tools mix namespaces
/// (PLCopen elements in tc6_0200, CODESYS device data without namespace).
/// </summary>
public static class SafeXml
{
    public static XmlReaderSettings Settings => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    /// <summary>Loads a document; invalid XML and DTDs end with exit 2 (XML_INVALID).</summary>
    public static XDocument Load(byte[] content, string displayPath)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var reader = XmlReader.Create(stream, Settings);
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new UsageException("XML_INVALID", $"Die Datei {displayPath} ist kein gültiges XML oder enthält eine DTD (Zeile {ex.LineNumber}): {ex.Message}", ex);
        }
    }

    public static IEnumerable<XElement> ElementsNamed(this XElement element, string localName) =>
        element.Elements().Where(e => e.Name.LocalName == localName);

    public static XElement? ElementNamed(this XElement element, string localName) =>
        element.ElementsNamed(localName).FirstOrDefault();

    /// <summary>Value of an unqualified attribute; empty or whitespace counts as missing.</summary>
    public static string? AttributeValue(this XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Trimmed text of a child element; empty counts as missing.</summary>
    public static string? ChildText(this XElement element, string localName)
    {
        var value = element.ElementNamed(localName)?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
