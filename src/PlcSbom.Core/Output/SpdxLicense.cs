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

    private static readonly Lazy<HashSet<string>> ListedIgnoreCase = new(() => Listed.Value.ToHashSet(StringComparer.OrdinalIgnoreCase));

    /// <summary>True for an identifier of the SPDX licence list (case-sensitive, as in the schema).</summary>
    public static bool IsListed(string id) => Listed.Value.Contains(id);

    /// <summary>
    /// TR-03183-2 section 6.1: an SPDX licence expression whose licence identifiers are on the SPDX list
    /// (matched without case, as SPDX does) or are LicenseRef-… / DocumentRef-…:LicenseRef-…. The
    /// identifier after WITH is an exception and is not checked. Free text is not a licence identifier.
    /// </summary>
    public static bool IsValidExpression(string text)
    {
        var tokens = text.Replace("(", " ", StringComparison.Ordinal).Replace(")", " ", StringComparison.Ordinal)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var identifiers = 0;
        var exceptionNext = false;
        foreach (var token in tokens)
        {
            if (token.Equals("AND", StringComparison.OrdinalIgnoreCase) || token.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (token.Equals("WITH", StringComparison.OrdinalIgnoreCase))
            {
                exceptionNext = true;
                continue;
            }
            if (exceptionNext)
            {
                exceptionNext = false;
                continue;
            }
            identifiers++;
            if (!IsValidIdentifier(token))
            {
                return false;
            }
        }
        return identifiers > 0 && !exceptionNext;
    }

    /// <summary>One licence identifier: on the SPDX list (a trailing "+" allowed) or a LicenseRef.</summary>
    public static bool IsValidIdentifier(string id) =>
        IsLicenseRef(id) || ListedIgnoreCase.Value.Contains(id.EndsWith('+') ? id[..^1] : id);

    private static bool IsLicenseRef(string id) =>
        (id.StartsWith("LicenseRef-", StringComparison.Ordinal) && id.Length > "LicenseRef-".Length)
        || (id.StartsWith("DocumentRef-", StringComparison.Ordinal) && id.Contains(":LicenseRef-", StringComparison.Ordinal));

    /// <summary>True for one identifier (listed or LicenseRef-…), false for an expression with operators.</summary>
    public static bool IsSingleIdentifier(string text) =>
        text.Length > 0 && !text.Any(c => char.IsWhiteSpace(c) || c is '(' or ')');
}
