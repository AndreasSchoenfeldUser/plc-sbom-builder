using System.Text.Json;
using PlcSbom.Core.Validation;

namespace PlcSbom.Core.Output;

/// <summary>SPDX licence identifiers as listed in the embedded spdx.schema.json of CycloneDX 1.6.2.</summary>
public static class SpdxLicense
{
    private static readonly Lazy<HashSet<string>> Listed = new(() =>
    {
        using var doc = JsonDocument.Parse(SchemaValidator.ReadResource("spdx.schema.json"));
        return doc.RootElement.GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
    });

    /// <summary>True for an identifier of the SPDX licence list (case-sensitive, as in the schema).</summary>
    public static bool IsListed(string id) => Listed.Value.Contains(id);

    /// <summary>True for one identifier (listed or LicenseRef-…), false for an expression with operators.</summary>
    public static bool IsSingleIdentifier(string text) =>
        text.Length > 0 && !text.Any(c => char.IsWhiteSpace(c) || c is '(' or ')');
}
