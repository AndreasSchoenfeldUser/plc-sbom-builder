namespace PlcSbom.Core.Output.Report;

// Content of the report, independent of its format. ReportBuilder fills it, HtmlReport and
// MarkdownReport render it. Every text is plain text; the renderers escape it.

public abstract record Block;

/// <summary>Section heading, level 2 or 3.</summary>
public sealed record Heading(int Level, string Text) : Block;

public sealed record Paragraph(string Text) : Block;

/// <summary>Label and value pairs, e.g. the head of the report.</summary>
public sealed record Facts(IReadOnlyList<KeyValuePair<string, string>> Rows) : Block;

public sealed record Table(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows) : Block;

/// <summary>Tree machine → station → component; nodes with children are foldable in HTML.</summary>
public sealed record Tree(TreeNode Root) : Block;

public sealed record TreeNode(string Text, string? Detail = null, IReadOnlyList<TreeNode>? Children = null)
{
    public IReadOnlyList<TreeNode> Children { get; init; } = Children ?? [];
}

/// <summary>The whole report.</summary>
public sealed record ReportDocument(string Title, bool Passed, string Verdict, IReadOnlyList<Block> Blocks);
