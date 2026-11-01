using System.Text;
using System.Text.RegularExpressions;
using PlcSbom.Core.FieldState;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Output.Report;
using PlcSbom.Core.Sbom;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Output;

public sealed partial class ReportTests
{
    /// <summary>The report build writes for the sample machine: with hardware appendix and tools per station.</summary>
    private static async Task<ReportDocument> BuildReport(bool releaseBuild = false)
    {
        var built = await SampleMachine.Get();
        return ReportBuilder.Build(new ReportInput
        {
            Sbom = built.View(),
            SbomPath = SampleMachine.SbomPath,
            Validation = built.Validation,
            ReleaseBuild = releaseBuild,
            Extras = built.Extras,
        });
    }

    /// <summary>The report of plc-sbom report with --field: SBOM alone, validated anew, comparison with 1042.</summary>
    private static async Task<ReportDocument> ReportWithField()
    {
        var built = await SampleMachine.Get();
        var view = built.View();
        return ReportBuilder.Build(new ReportInput
        {
            Sbom = view,
            SbomPath = SampleMachine.SbomPath,
            Validation = await SbomValidator.ValidateAsync(built.Json),
            Comparison = FieldComparer.Compare(view, SampleMachine.Field()),
        });
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public async Task Html_report_of_build_matches_golden_file()
    {
        Golden.AssertMatches("maschine-xy.bericht.html", HtmlReport.Write(await BuildReport()));
    }

    [Fact]
    public async Task Markdown_report_of_build_matches_golden_file()
    {
        Golden.AssertMatches("maschine-xy.bericht.md", MarkdownReport.Write(await BuildReport()));
    }

    [Fact]
    public async Task Markdown_report_with_field_comparison_matches_golden_file()
    {
        Golden.AssertMatches("maschine-xy-1042.bericht.md", MarkdownReport.Write(await ReportWithField()));
    }

    [Fact]
    public async Task Report_has_every_section_of_claude_md_8_2()
    {
        var report = await BuildReport();

        var headings = report.Blocks.OfType<Heading>().Select(h => h.Text).ToList();
        foreach (var expected in new[] { "Kennzahlen", "Quellen", "Maschinenbaum", "Komponenten je Station", "Station PLC_1", "Station Roboterzelle", "Station Verpackung", "Lücken", "Anhang: Hardware ohne eigene Firmware" })
        {
            Assert.Contains(expected, headings);
        }
        Assert.Equal("Prüfbericht Maschine XY, R2026.10", report.Title);
        Assert.True(report.Passed);
        Assert.Equal("bestanden (Entwicklungs-Build)", report.Verdict);
        var head = report.Blocks.OfType<Facts>().First().Rows.ToDictionary(r => r.Key, r => r.Value);
        Assert.Equal("17.10.2026 11:00 UTC", head["Erzeugt"]); // time of the SBOM, not of the report
        Assert.Equal("0 Fehler, 117 Warnungen, 109 Hinweise", head["Befunde"]);
    }

    [Fact]
    public async Task Release_build_report_does_not_pass_with_warnings()
    {
        var report = await BuildReport(releaseBuild: true);

        Assert.False(report.Passed);
        Assert.Equal("nicht bestanden (Release-Build)", report.Verdict);
    }

    [Fact]
    public async Task Tree_puts_the_controller_firmware_with_vendor_parts_under_its_station()
    {
        var tree = (await BuildReport()).Blocks.OfType<Tree>().Single().Root;

        var plc = tree.Children.Single(c => c.Text == "Station PLC_1");
        var controller = Assert.Single(plc.Children.Single(g => g.Text == "Steuerung").Children);
        Assert.Equal("CPU 1214C DC/DC/DC, Firmware", controller.Text);
        Assert.Equal(["Beispiel-Echtzeitkern", "Beispiel-TCP/IP-Stack"], controller.Children.Select(c => c.Text));
        Assert.Equal("Hardware ohne eigene Firmware (2)", plc.Children.Last().Text);
        var partial = plc.Children.Single(g => g.Text == "Teil-SBOMs").Children.Single();
        Assert.Equal(["ms", "picocolors"], partial.Children.Single().Children.Select(c => c.Text).Order(StringComparer.Ordinal));
        Assert.Equal("E/A-Module und Feldgeräte (10)", tree.Children.Single(c => c.Text == "Station Roboterzelle").Children.Single(g => g.Text.StartsWith("E/A", StringComparison.Ordinal)).Text);
    }

    [Fact]
    public async Task Gap_table_names_every_gap_with_what_closes_it()
    {
        var report = await BuildReport();

        var gaps = report.Blocks.OfType<Table>().Single(t => t.Header[0] == "Code");
        Assert.Equal(["Code", "Schwere", "Anzahl", "Komponenten", "Erklärung", "Was sie schließt"], gaps.Header);
        var projected = gaps.Rows.Single(r => r[0] == GapCatalog.FirmwareProjectedOnly);
        Assert.Equal(["firmware-projected-only", "Hinweis", "1", "plc-1-fw"], projected.Take(4));
        Assert.Contains("compare --as-built", projected[5], StringComparison.Ordinal);
        Assert.All(gaps.Rows, r => Assert.NotEqual("–", r[5]));
    }

    [Fact]
    public async Task Report_from_the_sbom_alone_explains_what_only_build_knows()
    {
        var report = await ReportWithField();

        var paragraphs = report.Blocks.OfType<Paragraph>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Diesen Anhang schreibt nur plc-sbom build", StringComparison.Ordinal));
        var sources = report.Blocks.OfType<Table>().Single(t => t.Header[0] == "Station" && t.Header[1] == "Adapter");
        Assert.All(sources.Rows, r => Assert.Equal("–", r[4]));
        Assert.Contains("Abgleich mit dem Feld", report.Blocks.OfType<Heading>().Select(h => h.Text));
    }

    [Fact]
    public async Task Html_report_has_no_external_resources_and_no_script()
    {
        var html = Text(HtmlReport.Write(await BuildReport()));

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(ExternalReference(), html);
        Assert.StartsWith("<!doctype html>\n<html lang=\"de\">", html);
        Assert.Contains("prefers-color-scheme: dark", html);
        Assert.DoesNotContain("\r", html);
    }

    [Fact]
    public void Html_and_markdown_escape_text()
    {
        var report = new ReportDocument("Test <b>", true, "bestanden",
        [
            new Table(["A"], [["<script>alert(1)</script>"], ["6ES7 590-1***0-0AA0 | x"]]),
        ]);

        var html = Text(HtmlReport.Write(report));
        var markdown = Text(MarkdownReport.Write(report));

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("Test &lt;b&gt;", html);
        Assert.Contains(@"6ES7 590-1\*\*\*0-0AA0 \| x", markdown);
        Assert.Contains(@"\<script\>", markdown);
    }

    [Fact]
    public void Foreign_sbom_gives_a_report_without_stations()
    {
        var bom = MinimalSbom.Create();
        MinimalSbom.RemoveProperty(bom["metadata"]!["component"]!.AsObject(), "plcsbom:logicalComponent");
        MinimalSbom.RemoveProperty(MinimalSbom.App(bom), "plcsbom:station");

        var report = ReportBuilder.Build(new ReportInput
        {
            Sbom = SbomView.From(bom),
            SbomPath = "fremd.cdx.json",
            Validation = new ValidationReport { SpecVersion = "1.6", SchemaValid = true, Findings = [] },
        });

        Assert.Contains(report.Blocks.OfType<Paragraph>(), p => p.Text == "Die SBOM nennt keine Stationen von plc-sbom.");
        Assert.Contains("Ohne Station", report.Blocks.OfType<Heading>().Select(h => h.Text));
        Assert.Contains(report.Blocks.OfType<Table>().SelectMany(t => t.Rows), r => r.Contains("Anwenderprogramm") && r.Contains("vollständig"));
    }

    [Fact]
    public async Task Two_runs_give_identical_bytes()
    {
        Assert.Equal(HtmlReport.Write(await BuildReport()), HtmlReport.Write(await BuildReport()));
    }

    /// <summary>src= or href= with a scheme, or url( in CSS.</summary>
    [GeneratedRegex(@"(src|href)\s*=\s*""?[a-z]+:|url\(", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalReference();
}
