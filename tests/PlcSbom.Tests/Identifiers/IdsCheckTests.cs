using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Identifiers;
using PlcSbom.Core.Sbom;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Identifiers;

public sealed class IdsCheckTests
{
    private const string LocalSiemens = "_local/csaf/siemens";
    private const string LocalOasis = "_local/csaf/oasis/bsi-2022-0001.json";

    private static CsafIndex Synthetic() => CsafIndex.Load(Fixtures.PathOf("csaf"));

    private static CpeMap SampleCpeMap() => CpeMap.Load(RepoPaths.Of("samples", "maschine-xy", "kennungen", "cpe-map.yml"), "kennungen/cpe-map.yml");

    // ---- CSAF index ----

    [Fact]
    public void Synthetic_csaf_document_is_read_with_vendor_branches_and_full_product_names()
    {
        var index = Synthetic();

        var document = Assert.Single(index.Documents);
        Assert.Equal(("kennungen-synthetisch.json", "PLCSBOM-TEST-0001", 5), (document.Path, document.TrackingId, document.Products));
        Assert.Equal(5, index.Products.Count); // product 4 has no identification helper
        Assert.Equal("Siemens", index.Products.Single(p => p.ProductId == "1").Vendor);
        Assert.Equal(["6ES7214-1AG40-0XB0"], index.Products.Single(p => p.ProductId == "1").ModelNumbers);
        Assert.Equal("Beckhoff", index.Products.Single(p => p.ProductId == "5").Vendor);
        var full = index.Products.Single(p => p.ProductId == "6");
        Assert.Null(full.Vendor);
        Assert.Equal("cpe:/a:vercel:ms:2.1.3", full.Cpe);
        Assert.Empty(index.Warnings);
    }

    [Fact]
    public void Files_that_are_no_csaf_are_skipped_with_a_warning()
    {
        var index = CsafIndex.Read(
        [
            ("kaputt.json", () => Encoding.UTF8.GetBytes("{")),
            ("sbom.json", () => Encoding.UTF8.GetBytes("""{ "bomFormat": "CycloneDX" }""")),
            ("ok.json", () => Fixtures.Read("csaf/kennungen-synthetisch.json")),
        ]);

        Assert.Single(index.Documents);
        Assert.Equal(["CSAF_INVALID", "CSAF_NOT_CSAF"], index.Warnings.Select(w => w.Code));
    }

    [Fact]
    public void Missing_directory_is_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => CsafIndex.Load(Path.Combine(Path.GetTempPath(), $"fehlt-{Guid.NewGuid():N}")));

        Assert.Equal("CSAF_DIR_NOT_FOUND", ex.Code);
    }

    // ---- Matching ----

    [Theory]
    [InlineData("6ES7214-1AG40-0XB0", "6ES7 214-1AG40-0XB0", true, false)]
    [InlineData("6es7214-1ag40-0xb0", "6ES7 214-1AG40-0XB0", true, false)]
    [InlineData("6ES7214", "6ES7 214-1AG40-0XB0", true, true)]
    [InlineData("6ES7511-1A?02", "6ES7 511-1AK02-0AB0", true, true)]
    [InlineData("6RA801?-??V62-0AA0", "6RA8013-6FV62-0AA0", true, true)]
    [InlineData("6ES7*-0XB0", "6ES7 214-1AG40-0XB0", true, true)]
    [InlineData("6ES7214-1AG40-0XB1", "6ES7 214-1AG40-0XB0", false, true)]
    [InlineData("1756-L85E", "6ES7 214-1AG40-0XB0", false, true)]
    public void Csaf_model_numbers_match_full_abbreviated_and_with_wildcards(string model, string orderNumber, bool matches, bool partial)
    {
        Assert.Equal(matches, IdsCheck.ModelMatches(model, orderNumber, out var isPartial));
        Assert.Equal(partial, isPartial);
    }

    [Theory]
    [InlineData("cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:*:*:*:*:*:*:*:*", "cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:4.5:*:*:*:*:*:*:*", true)]
    [InlineData("cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:4.5:*:*:*:*:*:*:*", "cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:4.6:*:*:*:*:*:*:*", false)]
    [InlineData("cpe:2.3:o:siemens:other_firmware:*:*:*:*:*:*:*:*", "cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:4.5:*:*:*:*:*:*:*", false)]
    [InlineData("cpe:/a:vercel:ms:2.1.3", "cpe:2.3:a:vercel:ms:2.1.3:*:*:*:*:node.js:*:*", true)]
    [InlineData("cpe:/a:Vercel:MS", "cpe:2.3:a:vercel:ms:2.1.3:*:*:*:*:*:*:*", true)]
    [InlineData("cpe:2.3:a:x:y:-:*:*:*:*:*:*:*", "cpe:2.3:a:x:y:1.0:*:*:*:*:*:*:*", false)]
    [InlineData("cpe:2.3:a:x:y:4\\.5:*:*:*:*:*:*:*", "cpe:2.3:a:x:y:4.5:*:*:*:*:*:*:*", true)]
    [InlineData("kein-cpe", "cpe:2.3:a:x:y:1:*:*:*:*:*:*:*", false)]
    public void Cpe_match_in_2_3_and_2_2_form(string csaf, string sbom, bool matches)
    {
        Assert.Equal(matches, CpeMatch.Matches(csaf, sbom));
    }

    // ---- Coverage ----

    [Fact]
    public async Task Sample_machine_against_synthetic_csaf()
    {
        var report = IdsCheck.Run((await SampleMachine.Get()).View(), Synthetic(), SampleCpeMap());

        Assert.Equal(48, report.Entries.Count);
        Assert.Equal(4, report.WithIdentifier);
        Assert.Equal(2, report.Count(IdsStatus.Found));
        Assert.Equal(1, report.Count(IdsStatus.Pattern));
        var cpu = report.Entries.Single(e => e.Component.BomRef == "plc-1-fw");
        Assert.Equal(IdsStatus.Found, cpu.Status);
        Assert.Equal(["model_numbers", "cpe"], cpu.Hits.Select(h => h.Via));
        Assert.True(cpu.InCpeMap);
        Assert.Equal(IdsStatus.Found, report.Entries.Single(e => e.Component.Name == "ms").Status); // CPE 2.2 in CSAF
        // The EK1100 is in the CSAF document, but the project gives no order number to look for.
        Assert.Equal(IdsStatus.None, report.Entries.Single(e => e.Component.BomRef == "roboterzelle-fw-device-1-ethercat-1").Status);
        Assert.False(report.Entries.Single(e => e.Component.BomRef == "plc-1-fw-gsd-device-1-0-0").InCpeMap);
    }

    [Fact]
    public async Task Coverage_report_matches_golden_file()
    {
        var report = IdsCheck.Run((await SampleMachine.Get()).View(), Synthetic(), SampleCpeMap());

        Golden.AssertMatches("maschine-xy.ids.json", IdsCheck.WriteJson(report, SampleMachine.SbomPath));
    }

    [Fact]
    public void Foreign_sbom_without_identifiers_has_only_none()
    {
        var report = IdsCheck.Run(SbomView.From(MinimalSbom.Create()), Synthetic(), cpeMap: null);

        Assert.Equal(IdsStatus.None, Assert.Single(report.Entries).Status);
        Assert.Null(report.Entries[0].InCpeMap);
        Assert.False(report.CpeMapGiven);
    }

    // ---- Local CSAF documents without licence (fixtures/_local/csaf) ----

    [Fact]
    public void Local_siemens_documents_carry_model_numbers_and_no_cpe()
    {
        Assert.SkipUnless(Directory.Exists(Fixtures.PathOf(LocalSiemens)), $"Lokale Fixtures {LocalSiemens} fehlen (fixtures/FIXTURES.md).");

        var index = CsafIndex.Load(Fixtures.PathOf(LocalSiemens));

        Assert.Equal(6, index.Documents.Count);
        Assert.Equal(404, index.Products.Count);
        Assert.All(index.Products, p => Assert.Null(p.Cpe));
        Assert.All(index.Products, p => Assert.DoesNotContain(" ", p.ModelNumbers[0], StringComparison.Ordinal));
        Assert.All(index.Products, p => Assert.Equal("Siemens", p.Vendor));
    }

    [Fact]
    public async Task Local_siemens_documents_find_the_cpu_of_the_sample_machine()
    {
        Assert.SkipUnless(Directory.Exists(Fixtures.PathOf(LocalSiemens)), $"Lokale Fixtures {LocalSiemens} fehlen (fixtures/FIXTURES.md).");

        var report = IdsCheck.Run((await SampleMachine.Get()).View(), CsafIndex.Load(Fixtures.PathOf(LocalSiemens)), cpeMap: null);

        var cpu = report.Entries.Single(e => e.Component.BomRef == "plc-1-fw");
        Assert.Equal(["SSA-224824", "SSA-717113", "SSA-876787"], cpu.Hits.Select(h => h.Document).Distinct().Order(StringComparer.Ordinal));
        Assert.All(cpu.Hits, h => Assert.False(h.Partial));
        Assert.Equal(1, report.Count(IdsStatus.Found));
    }

    [Fact]
    public void Local_oasis_example_carries_cpe_2_2()
    {
        Assert.SkipUnless(File.Exists(Fixtures.PathOf(LocalOasis)), $"Lokale Fixture {LocalOasis} fehlt (fixtures/FIXTURES.md).");

        var index = CsafIndex.Read([("bsi-2022-0001.json", () => Fixtures.Read(LocalOasis))]);

        Assert.Equal(6, index.Products.Count(p => p.Cpe is not null));
        Assert.All(index.Products, p => Assert.StartsWith("cpe:/a:csaf-tools:cvrf-csaf-converter:", p.Cpe, StringComparison.Ordinal));
        Assert.True(CpeMatch.Matches(index.Products[0].Cpe!, "cpe:2.3:a:csaf-tools:cvrf-csaf-converter:1.0.0-alpha:*:*:*:*:*:*:*"));
    }
}
