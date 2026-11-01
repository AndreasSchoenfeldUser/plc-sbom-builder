using System.Text.Json.Nodes;
using PlcSbom.Cli;
using PlcSbom.Core;
using PlcSbom.Tests.Build;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Cli;

/// <summary>plc-sbom report, compare and ids end to end on the SBOM of the sample machine.</summary>
public sealed class FieldCommandsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("plc-sbom-cli-m10-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string P(string name) => Path.Combine(_dir, name);

    private async Task<string> Sbom()
    {
        var path = P("sbom-maschine.cdx.json");
        await File.WriteAllBytesAsync(path, (await SampleMachine.Get()).Sbom, TestContext.Current.CancellationToken);
        return path;
    }

    private static async Task<(int Exit, string Out, string Err)> Run(FakeBuildEnvironment? environment, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(args, new CliEnvironment(IsWindows: false) { Build = environment ?? new FakeBuildEnvironment() }, stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    // ---- report ----

    [Theory]
    [InlineData("html", "<!doctype html>")]
    [InlineData("md", "# Prüfbericht Maschine XY, R2026.10")]
    public async Task Report_writes_the_file_and_ends_with_exit_0(string format, string start)
    {
        var sbom = await Sbom();

        var (exit, output, error) = await Run(null, "report", sbom, "--format", format, "--out", P($"bericht.{format}"));

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        Assert.StartsWith(start, await File.ReadAllTextAsync(P($"bericht.{format}"), TestContext.Current.CancellationToken));
        Assert.Contains("Prüfung bestanden (Entwicklungs-Build)", output);
    }

    [Fact]
    public async Task Report_with_field_and_release_build()
    {
        var sbom = await Sbom();

        var (exit, output, _) = await Run(null, "report", sbom, "--format", "md", "--out", P("b.md"), "--field", SampleMachine.FieldPath, "--release-build");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Contains("Prüfung nicht bestanden (Release-Build)", output);
        Assert.Contains("[abgleich]  13 geprüft: 2 gleich, 1 abweichend, 10 nur im Projekt, 1 nur im Feld, 1 passiv", output);
        Assert.Contains("## Abgleich mit dem Feld", await File.ReadAllTextAsync(P("b.md"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Report_of_a_missing_or_broken_sbom_is_exit_2()
    {
        await File.WriteAllTextAsync(P("kaputt.json"), "{", TestContext.Current.CancellationToken);

        var missing = await Run(null, "report", P("fehlt.json"), "--format", "md", "--out", P("b.md"));
        var broken = await Run(null, "report", P("kaputt.json"), "--format", "md", "--out", P("b.md"));

        Assert.Equal((ExitCodes.Usage, ExitCodes.Usage), (missing.Exit, broken.Exit));
        Assert.StartsWith("[SBOM_NOT_FOUND] ", missing.Err);
        Assert.StartsWith("[SBOM_INVALID] ", broken.Err);
    }

    // ---- compare ----

    [Fact]
    public async Task Compare_with_deviations_is_exit_1_and_writes_as_built_csv_and_result()
    {
        var sbom = await Sbom();
        var environment = new FakeBuildEnvironment(); // no SOURCE_DATE_EPOCH: the as-built SBOM takes the field date

        var (exit, output, error) = await Run(environment, "compare", sbom, "--field", SampleMachine.FieldPath, "--as-built", P("sbom-1042.cdx.json"), "--csv", P("abgleich.csv"));

        Assert.Equal(ExitCodes.Finding, exit);
        Assert.Equal("", error);
        Assert.Contains("[abgleich]  abweichend      PLC_1 S7-1200 station_1/0/1 CPU 1214C DC/DC/DC: Firmware im Feld V4.6.1, in der SBOM V4.5.", output);
        Assert.Contains("[as-built]  Seriennummer 1042: 3 Firmware-Stände aus dem Feld übernommen", output);
        Assert.Contains("[prüfung]   as-built-SBOM: Schema CycloneDX 1.6 OK, 0 Fehler,", output);
        Assert.DoesNotContain("[abgleich]  gleich", output); // equal devices only with --verbose
        Assert.True(File.Exists(P("abgleich.csv")));

        var asBuilt = JsonNode.Parse(await File.ReadAllTextAsync(P("sbom-1042.cdx.json"), TestContext.Current.CancellationToken))!;
        Assert.Equal("2026-10-22T00:00:00Z", asBuilt["metadata"]!["timestamp"]!.GetValue<string>());

        var result = JsonNode.Parse(await File.ReadAllTextAsync(P("sbom-maschine.compare-1042.result.json"), TestContext.Current.CancellationToken))!;
        Assert.Equal("compare", result["command"]!.GetValue<string>());
        Assert.Equal(1, result["exitCode"]!.GetValue<int>());
        var comparison = result["comparison"]!;
        Assert.Equal(12, comparison["deviations"]!.GetValue<int>());
        Assert.Equal(13, comparison["checked"]!.GetValue<int>());
        Assert.Equal(12, comparison["deviationList"]!.AsArray().Count);
        Assert.Equal("abweichend", comparison["deviationList"]![0]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Compare_as_built_uses_source_date_epoch_when_set()
    {
        var sbom = await Sbom();

        await Run(FakeBuildEnvironment.Reproducible(), "compare", sbom, "--field", SampleMachine.FieldPath, "--as-built", P("a.cdx.json"));

        var asBuilt = JsonNode.Parse(await File.ReadAllTextAsync(P("a.cdx.json"), TestContext.Current.CancellationToken))!;
        Assert.Equal("2026-10-17T11:00:00Z", asBuilt["metadata"]!["timestamp"]!.GetValue<string>());
    }

    [Fact]
    public async Task Compare_without_deviations_is_exit_0()
    {
        var sbom = await Sbom();
        var field = P("gleich.json");
        await File.WriteAllTextAsync(field, """
            { "machine": "Maschine XY", "serialNumber": "1", "date": "2026-10-22", "devices": [] }
            """, TestContext.Current.CancellationToken);
        // Every device of the SBOM would be "nur-im-projekt"; a SBOM without devices has none.
        var bom = JsonNode.Parse(await File.ReadAllTextAsync(sbom, TestContext.Current.CancellationToken))!.AsObject();
        foreach (var c in bom["components"]!.AsArray().OfType<JsonObject>().Where(c => c["type"]!.GetValue<string>() == "firmware").ToList())
        {
            bom["components"]!.AsArray().Remove(c);
        }
        await File.WriteAllTextAsync(sbom, bom.ToJsonString(), TestContext.Current.CancellationToken);

        var (exit, output, _) = await Run(null, "compare", sbom, "--field", field, "--verbose");

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Contains("0 geprüft: 0 gleich, 0 abweichend", output);
    }

    [Fact]
    public async Task Compare_with_invalid_field_file_is_exit_2()
    {
        var sbom = await Sbom();
        await File.WriteAllTextAsync(P("feld.json"), """{ "machine": "Maschine XY" }""", TestContext.Current.CancellationToken);

        var (exit, _, error) = await Run(null, "compare", sbom, "--field", P("feld.json"));

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.StartsWith("[FIELD_INVALID] ", error);
    }

    // ---- ids ----

    [Fact]
    public async Task Ids_reports_coverage_with_exit_0_and_writes_json()
    {
        var sbom = await Sbom();

        var (exit, output, error) = await Run(null, "ids", sbom, "--csaf", Fixtures.PathOf("csaf"),
            "--cpe-map", RepoPaths.Of("samples", "maschine-xy", "kennungen", "cpe-map.yml"), "--out", P("ids.json"));

        Assert.Equal(ExitCodes.Ok, exit);
        Assert.Equal("", error);
        Assert.Contains(": 1 Dokument, 3 Produkte mit model_numbers, 2 mit CPE", output);
        Assert.Contains("[kennung]   gefunden        plc-1-fw (CPU 1214C DC/DC/DC, Firmware) 6ES7 214-1AG40-0XB0: PLCSBOM-TEST-0001; CPE-Tabelle: Eintrag vorhanden", output);
        Assert.Contains("[abdeckung] 2 von 4 Komponenten mit Kennung in CSAF gefunden (50 %); 1 mit Bestellnummer als Muster, 43 ohne Kennung", output);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(P("ids.json"), TestContext.Current.CancellationToken))!;
        Assert.Equal(2, json["coverage"]!["found"]!.GetValue<int>());
    }

    [Fact]
    public async Task Ids_with_a_directory_without_csaf_is_exit_2()
    {
        var sbom = await Sbom();
        Directory.CreateDirectory(P("leer"));
        await File.WriteAllTextAsync(P("leer/x.json"), "{}", TestContext.Current.CancellationToken);

        var (exit, _, error) = await Run(null, "ids", sbom, "--csaf", P("leer"));

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("[CSAF_NOT_CSAF] ", error);
        Assert.Contains("[CSAF_NONE] ", error);
    }
}
