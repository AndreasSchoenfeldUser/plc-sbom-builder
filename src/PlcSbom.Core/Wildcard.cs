namespace PlcSbom.Core;

/// <summary>
/// Patterns from machine.yml with '*' (any number of characters) and '?' (exactly one character),
/// compared ordinal and case-sensitive over the whole text (devices, ignoreDevices, passiveOrderNumbers,
/// vendorOverrides).
/// </summary>
public static class Wildcard
{
    public static bool IsMatch(string pattern, string text) => Match(pattern.AsSpan(), text.AsSpan());

    /// <summary>
    /// Order numbers match with or without spaces: "6ES7511*" and "6ES7 511*" both match "6ES7 511-1AK02-0AB0".
    /// The source keeps its spaces (CLAUDE.md section 4.3); only the comparison ignores them.
    /// </summary>
    public static bool IsOrderNumberMatch(string pattern, string orderNumber) =>
        IsMatch(pattern, orderNumber) || IsMatch(pattern.Replace(" ", "", StringComparison.Ordinal), orderNumber.Replace(" ", "", StringComparison.Ordinal));

    private static bool Match(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text)
    {
        // Iterative matcher with backtracking to the last '*'.
        int p = 0, t = 0, star = -1, mark = 0;
        while (t < text.Length)
        {
            // '*' first: order numbers such as 6ES7 590-1***0-0AA0 contain '*' as text.
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == text[t]))
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }
        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }
        return p == pattern.Length;
    }
}
