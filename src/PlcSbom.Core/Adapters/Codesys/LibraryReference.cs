using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PlcSbom.Core.Adapters.Codesys;

/// <summary>
/// A CODESYS library reference in the form "Title, Version (Company)", e.g.
/// "SM3_Basic, 4.5.1.0 (3S - Smart Software Solutions GmbH)" or "Standard, * (System)".
/// Belegt in all PLCopen fixtures (Library@Name, @DefaultResolution, PlaceholderRedirection@Redirection).
/// </summary>
public sealed partial record LibraryReference(string Title, string Version, string Company)
{
    /// <summary>"*" means "newest installed version" (CODESYS help, Library Manager).</summary>
    [JsonIgnore]
    public bool IsNewest => Version == "*";

    /// <summary>Parses the reference; null if the text does not follow the pattern.</summary>
    public static LibraryReference? Parse(string? text)
    {
        if (text is null)
        {
            return null;
        }
        var match = Pattern().Match(text.Trim());
        return match.Success
            ? new LibraryReference(match.Groups["title"].Value.Trim(), match.Groups["version"].Value, match.Groups["company"].Value.Trim())
            : null;
    }

    public override string ToString() => $"{Title}, {Version} ({Company})";

    // Title may contain commas, so it is greedy; version is the last ", <token>" before " (Company)".
    [GeneratedRegex(@"^(?<title>.+), (?<version>\*|[0-9][0-9A-Za-z.\-]*) \((?<company>[^()]+)\)$")]
    private static partial Regex Pattern();
}
