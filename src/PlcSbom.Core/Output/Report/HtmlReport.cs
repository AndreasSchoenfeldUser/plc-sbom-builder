using System.Net;
using System.Text;

namespace PlcSbom.Core.Output.Report;

/// <summary>
/// The report as one HTML file without external resources (CLAUDE.md section 8.2: the agent is offline and
/// the file is archived). No script; the tree folds with &lt;details&gt;. Light and dark from the system
/// setting. LF line endings, no BOM.
/// </summary>
public static class HtmlReport
{
    private const string Style = """
        :root{--bg:#ffffff;--fg:#1d2127;--muted:#5b6470;--line:#d9dde3;--head:#f3f5f8;--ok:#1d7a3e;--ok-bg:#e3f4e8;--bad:#b42318;--bad-bg:#fde8e6;--accent:#1f5fa8}
        @media (prefers-color-scheme: dark){:root{--bg:#15181d;--fg:#e4e7eb;--muted:#9aa3ad;--line:#343a43;--head:#1e232a;--ok:#6fd08f;--ok-bg:#183424;--bad:#ff8a7d;--bad-bg:#3d1c19;--accent:#7fb2ef}}
        *{box-sizing:border-box}
        body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.5 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
        main{max-width:1200px;margin:0 auto;padding:24px 16px 48px}
        h1{font-size:1.6rem;margin:0 0 4px}
        h2{font-size:1.25rem;margin:32px 0 8px;padding-bottom:4px;border-bottom:1px solid var(--line)}
        h3{font-size:1.05rem;margin:20px 0 6px}
        p{margin:8px 0}
        .verdict{display:inline-block;margin:4px 0 12px;padding:2px 10px;border-radius:12px;font-weight:600}
        .verdict.ok{color:var(--ok);background:var(--ok-bg)}
        .verdict.bad{color:var(--bad);background:var(--bad-bg)}
        dl{display:grid;grid-template-columns:max-content 1fr;gap:2px 16px;margin:8px 0}
        dt{color:var(--muted)}
        dd{margin:0;overflow-wrap:anywhere}
        .scroll{overflow-x:auto;margin:8px 0}
        table{border-collapse:collapse;width:100%;font-size:.9rem}
        th,td{border:1px solid var(--line);padding:4px 8px;text-align:left;vertical-align:top;overflow-wrap:anywhere}
        th{background:var(--head);font-weight:600}
        ul.tree{list-style:none;padding-left:0;margin:8px 0}
        ul.tree ul{list-style:none;padding-left:20px;margin:0;border-left:1px solid var(--line)}
        ul.tree li{margin:2px 0;padding-left:6px}
        summary{cursor:pointer}
        .detail{color:var(--muted)}
        footer{margin-top:32px;color:var(--muted);font-size:.85rem}
        @media print{details{display:block}summary{list-style:none}.scroll{overflow:visible}}
        """;

    public static byte[] Write(ReportDocument report)
    {
        var b = new StringBuilder();
        b.Append("<!doctype html>\n<html lang=\"de\">\n<head>\n<meta charset=\"utf-8\">\n");
        b.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        b.Append("<title>").Append(E(report.Title)).Append("</title>\n");
        b.Append("<style>\n").Append(Style.Replace("\r\n", "\n", StringComparison.Ordinal)).Append("\n</style>\n</head>\n<body>\n<main>\n");
        b.Append("<h1>").Append(E(report.Title)).Append("</h1>\n");
        b.Append("<div class=\"verdict ").Append(report.Passed ? "ok" : "bad").Append("\">Prüfung ").Append(E(report.Verdict)).Append("</div>\n");
        for (var i = 0; i < report.Blocks.Count; i++)
        {
            var last = i == report.Blocks.Count - 1;
            switch (report.Blocks[i])
            {
                case Heading h:
                    b.Append($"<h{h.Level}>").Append(E(h.Text)).Append($"</h{h.Level}>\n");
                    break;
                case Paragraph p when last:
                    b.Append("<footer>").Append(E(p.Text)).Append("</footer>\n");
                    break;
                case Paragraph p:
                    b.Append("<p>").Append(E(p.Text)).Append("</p>\n");
                    break;
                case Facts f:
                    b.Append("<dl>\n");
                    foreach (var (label, value) in f.Rows)
                    {
                        b.Append("<dt>").Append(E(label)).Append("</dt><dd>").Append(E(value)).Append("</dd>\n");
                    }
                    b.Append("</dl>\n");
                    break;
                case Table t:
                    b.Append("<div class=\"scroll\"><table>\n<thead><tr>");
                    foreach (var cell in t.Header)
                    {
                        b.Append("<th>").Append(E(cell)).Append("</th>");
                    }
                    b.Append("</tr></thead>\n<tbody>\n");
                    foreach (var row in t.Rows)
                    {
                        b.Append("<tr>");
                        foreach (var cell in row)
                        {
                            b.Append("<td>").Append(E(cell)).Append("</td>");
                        }
                        b.Append("</tr>\n");
                    }
                    b.Append("</tbody>\n</table></div>\n");
                    break;
                case Tree tree:
                    b.Append("<ul class=\"tree\">\n");
                    Node(b, tree.Root, depth: 0);
                    b.Append("</ul>\n");
                    break;
                default:
                    throw new InvalidOperationException($"Unbekannter Berichtsbaustein {report.Blocks[i].GetType().Name}.");
            }
        }
        b.Append("</main>\n</body>\n</html>\n");
        return Encoding.UTF8.GetBytes(b.ToString());
    }

    private static void Node(StringBuilder b, TreeNode node, int depth)
    {
        var label = new StringBuilder(E(node.Text));
        if (node.Detail is not null)
        {
            label.Append(" <span class=\"detail\">").Append(E(node.Detail)).Append("</span>");
        }
        if (node.Children.Count == 0)
        {
            b.Append("<li>").Append(label).Append("</li>\n");
            return;
        }
        // Machine and stations open; groups below closed, so a machine with many terminals stays readable.
        b.Append("<li><details").Append(depth < 2 ? " open" : "").Append("><summary>").Append(label).Append("</summary>\n<ul>\n");
        foreach (var child in node.Children)
        {
            Node(b, child, depth + 1);
        }
        b.Append("</ul></details></li>\n");
    }

    private static string E(string text) => WebUtility.HtmlEncode(text);
}
