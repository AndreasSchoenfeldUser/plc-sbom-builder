using System.Text;

namespace PlcSbom.Core.Output.Report;

/// <summary>The report as Markdown (GitHub flavour): tables, the tree as nested list. LF line endings, no BOM.</summary>
public static class MarkdownReport
{
    public static byte[] Write(ReportDocument report)
    {
        var b = new StringBuilder();
        b.Append("# ").Append(E(report.Title)).Append("\n\n");
        b.Append("**Prüfung ").Append(E(report.Verdict)).Append("**\n\n");
        foreach (var block in report.Blocks)
        {
            switch (block)
            {
                case Heading h:
                    b.Append(new string('#', h.Level)).Append(' ').Append(E(h.Text)).Append("\n\n");
                    break;
                case Paragraph p:
                    b.Append(E(p.Text)).Append("\n\n");
                    break;
                case Facts f:
                    b.Append("| | |\n|---|---|\n");
                    foreach (var (label, value) in f.Rows)
                    {
                        b.Append("| ").Append(Cell(label)).Append(" | ").Append(Cell(value)).Append(" |\n");
                    }
                    b.Append('\n');
                    break;
                case Table t:
                    b.Append("| ").AppendJoin(" | ", t.Header.Select(Cell)).Append(" |\n");
                    b.Append('|').AppendJoin("", t.Header.Select(_ => "---|")).Append('\n');
                    foreach (var row in t.Rows)
                    {
                        b.Append("| ").AppendJoin(" | ", row.Select(Cell)).Append(" |\n");
                    }
                    b.Append('\n');
                    break;
                case Tree tree:
                    Node(b, tree.Root, 0);
                    b.Append('\n');
                    break;
                default:
                    throw new InvalidOperationException($"Unbekannter Berichtsbaustein {block.GetType().Name}.");
            }
        }
        return Encoding.UTF8.GetBytes(b.ToString().TrimEnd('\n') + "\n");
    }

    private static void Node(StringBuilder b, TreeNode node, int depth)
    {
        b.Append(new string(' ', depth * 2)).Append("- ").Append(E(node.Text));
        if (node.Detail is not null)
        {
            b.Append(": ").Append(E(node.Detail));
        }
        b.Append('\n');
        foreach (var child in node.Children)
        {
            Node(b, child, depth + 1);
        }
    }

    private static string Cell(string text) => E(text).Replace("|", "\\|", StringComparison.Ordinal);

    /// <summary>Characters that Markdown would read as markup, e.g. the '*' of 6ES7 590-1***0-0AA0.</summary>
    private static string E(string text)
    {
        var b = new StringBuilder(text.Length);
        foreach (var c in text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' '))
        {
            if (c is '\\' or '*' or '_' or '`' or '[' or ']' or '<' or '>' or '#')
            {
                b.Append('\\');
            }
            b.Append(c);
        }
        return b.ToString();
    }
}
