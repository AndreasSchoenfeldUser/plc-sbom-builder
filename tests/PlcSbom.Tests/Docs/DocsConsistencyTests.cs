using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PlcSbom.Core.Gaps;

namespace PlcSbom.Tests.Docs;

/// <summary>
/// CLAUDE.md section 14: every tool change updates CLAUDE.md, README.md and ANLEITUNG.md. These tests
/// catch the part that can be checked mechanically: gap codes and their severity.
/// </summary>
public sealed partial class DocsConsistencyTests
{
    private static string RepoFile(string name, [CallerFilePath] string here = "") =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", name)));

    public static TheoryData<string> GapCodes => new(GapCatalog.All.Select(g => g.Code));

    [Theory]
    [MemberData(nameof(GapCodes))]
    public void Anleitung_lists_every_gap_code_with_its_severity(string code)
    {
        var row = Assert.Single(TableRows(RepoFile("ANLEITUNG.md")), r => r.Code == code);
        var expected = GapCatalog.Find(code)!.Severity == Severity.Warning ? "Warnung" : "Hinweis";

        Assert.Equal(expected, row.Severity);
    }

    [Theory]
    [MemberData(nameof(GapCodes))]
    public void Claude_md_lists_every_gap_code(string code)
    {
        Assert.Contains(TableRows(RepoFile("CLAUDE.md")), r => r.Code == code);
    }

    [Fact]
    public void Anleitung_lists_no_gap_code_the_tool_does_not_know()
    {
        var known = GapCatalog.All.Select(g => g.Code).ToHashSet();
        var listed = TableRows(RepoFile("ANLEITUNG.md")).Select(r => r.Code);

        Assert.All(listed, code => Assert.Contains(code, known));
    }

    [Theory]
    [InlineData("ANLEITUNG.md")]
    [InlineData("README.md")]
    public void User_docs_state_the_tool_version(string file)
    {
        Assert.Contains(PlcSbom.Core.ToolVersion.Short, RepoFile(file));
    }

    /// <summary>Rows of gap tables: first cell is a backticked kebab-case code, one cell says Hinweis or Warnung.</summary>
    private static List<(string Code, string Severity)> TableRows(string markdown) =>
        GapRow().Matches(markdown)
            .Select(m => (m.Groups["code"].Value, m.Groups["severity"].Value))
            .ToList();

    [GeneratedRegex(@"^\| `(?<code>[a-z0-9]+(?:-[a-z0-9]+)+)` \|(?:[^|\n]*\|)*? (?<severity>Hinweis|Warnung)\b[^|\n]*\|", RegexOptions.Multiline)]
    private static partial Regex GapRow();
}
