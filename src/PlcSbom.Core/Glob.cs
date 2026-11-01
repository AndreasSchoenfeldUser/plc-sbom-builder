namespace PlcSbom.Core;

/// <summary>
/// File patterns from machine.yml ("vendorSboms"), relative to a base directory: '*' and '?' within one
/// path segment, "**" for any number of directories. Separator '/' (on Windows also '\'). Results are
/// absolute paths sorted ordinal by their relative path, so every platform sees the same order.
/// </summary>
public static class Glob
{
    public static IReadOnlyList<string> Find(string baseDirectory, string pattern)
    {
        var segments = pattern.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fixedCount = segments.TakeWhile(s => !IsWildcard(s)).Count();
        if (fixedCount == segments.Length)
        {
            var single = Path.GetFullPath(Path.Combine([baseDirectory, .. segments]));
            return File.Exists(single) ? [single] : [];
        }
        var start = Path.GetFullPath(Path.Combine([baseDirectory, .. segments.Take(fixedCount)]));
        if (!Directory.Exists(start))
        {
            return [];
        }
        var rest = segments.Skip(fixedCount).ToArray();
        return Directory.EnumerateFiles(start, "*", SearchOption.AllDirectories)
            .Select(f => (Full: f, Relative: Path.GetRelativePath(start, f).Replace('\\', '/')))
            .Where(f => Match(rest, f.Relative.Split('/')))
            .OrderBy(f => f.Relative, StringComparer.Ordinal)
            .Select(f => f.Full)
            .ToList();
    }

    private static bool IsWildcard(string segment) => segment.Contains('*', StringComparison.Ordinal) || segment.Contains('?', StringComparison.Ordinal);

    private static bool Match(ReadOnlySpan<string> pattern, ReadOnlySpan<string> path)
    {
        if (pattern.Length == 0)
        {
            return path.Length == 0;
        }
        if (pattern[0] == "**")
        {
            for (var skip = 0; skip <= path.Length; skip++)
            {
                if (Match(pattern[1..], path[skip..]))
                {
                    return true;
                }
            }
            return false;
        }
        return path.Length > 0 && Wildcard.IsMatch(pattern[0], path[0]) && Match(pattern[1..], path[1..]);
    }
}
