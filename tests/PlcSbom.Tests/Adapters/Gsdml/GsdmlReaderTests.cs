using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.Gsdml;

namespace PlcSbom.Tests.Adapters.Gsdml;

public sealed class GsdmlReaderTests
{
    public const string Leuze = "gsdml/GSDML-V2.41-LEUZE-BCL248i-20211213.xml";

    /// <summary>File name as TIA writes it into the TypeIdentifier of the V17 AML fixture (upper case).</summary>
    public const string LeuzeInAml = "GSDML-V2.41-LEUZE-BCL248I-20211213.XML";

    public static GsdmlIndex FixtureIndex() => GsdmlIndex.Load([(Fixtures.PathOf("gsdml"), "gsdml")]);

    /// <summary>A fresh directory under the temp path, deleted when disposed.</summary>
    public sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("plc-sbom-gsdml-").FullName;

        public string Write(string relative, byte[] content)
        {
            var full = System.IO.Path.Combine(Path, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, content);
            return full;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    [Fact]
    public void Reads_identity_and_vendor()
    {
        var file = GsdmlReader.Read(Fixtures.Read(Leuze), Leuze);

        Assert.Equal(Leuze, file.File);
        Assert.Equal("ca1b97e3068ce1663ebba66422d9fbf77d314fc8ff3dfe589ad8c0bf6156eaf7", file.Sha256);
        Assert.Equal(("0x0152", "0x0015"), (file.VendorId, file.DeviceId));
        Assert.Equal("Leuze electronic GmbH + Co. KG", file.VendorName);
    }

    [Fact]
    public void Reads_the_device_access_point_from_its_own_module_info()
    {
        var file = GsdmlReader.Read(Fixtures.Read(Leuze), Leuze);

        var dap = Assert.Single(file.AccessPoints.Values);
        Assert.Equal(new GsdmlItem("DAP", "501xxxxx", "V1.*.*", "3"), dap);
    }

    [Fact]
    public void Reads_modules_without_order_number()
    {
        var file = GsdmlReader.Read(Fixtures.Read(Leuze), Leuze);

        Assert.Equal(57, file.Modules.Count);
        Assert.Equal(new GsdmlItem("ID_MOD_Activation", null, null, null), file.Modules["ID_MOD_Activation"]);
        Assert.Contains("ID_MOD_ReadingGateControl", file.Modules.Keys);
    }

    [Fact]
    public void Module_order_number_is_read_where_the_module_info_has_one()
    {
        // The real file has OrderNumber only in the DAP's ModuleInfo; the same element in a module.
        var xml = Encoding.Latin1.GetString(Fixtures.Read(Leuze));
        const string info = "<Name TextId=\"IDT_MODULE_NAME_Activation\" />";
        Assert.Contains(info, xml);
        var changed = xml.Replace(info, info + "<OrderNumber Value=\"50112345\" /><SoftwareRelease Value=\"V1.2.0\" />", StringComparison.Ordinal);

        var module = GsdmlReader.Read(Encoding.Latin1.GetBytes(changed), "x.xml").Modules["ID_MOD_Activation"];

        Assert.Equal(("50112345", "V1.2.0"), (module.OrderNumber, module.SoftwareRelease));
    }

    [Theory]
    [InlineData("501xxxxx", true)]
    [InlineData("6ES7 590-1***0-0AA0", true)]
    [InlineData("50?12345", true)]
    [InlineData("50132079", false)]
    [InlineData("6ES7 214-1AG40-0XB0", false)]
    [InlineData("BCL 248i", false)]
    public void Order_number_pattern_is_recognised(string orderNumber, bool expected)
    {
        Assert.Equal(expected, GsdmlFile.IsOrderNumberPattern(orderNumber));
    }

    [Fact]
    public void Other_xml_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => GsdmlReader.Read(Fixtures.Read("tia-aml/plc-commissioning-sample_device.aml"), "x.xml"));

        Assert.Equal("NOT_GSDML", ex.Code);
        Assert.Contains("CAEXFile", ex.Message);
    }

    // ---- Index ----------------------------------------------------------------------------------------

    [Fact]
    public void Index_finds_the_file_without_case()
    {
        var lookup = FixtureIndex().Find(LeuzeInAml);

        Assert.Equal(Leuze, lookup.Found!.File);
        Assert.Equal([Leuze], lookup.Candidates);
    }

    [Fact]
    public void Index_without_the_file_finds_nothing()
    {
        var lookup = FixtureIndex().Find("GSDML-V2.35-GIBT-ES-NICHT.xml");

        Assert.Null(lookup.Found);
        Assert.False(lookup.IsAmbiguous);
    }

    [Fact]
    public void Index_searches_subdirectories_and_parses_only_the_file_asked_for()
    {
        using var dir = new TempDir();
        dir.Write("leuze/2021/" + LeuzeInAml.ToLowerInvariant(), Fixtures.Read(Leuze));
        dir.Write("kaputt.xml", Encoding.UTF8.GetBytes("kein XML"));

        var lookup = GsdmlIndex.Load([(dir.Path, "gsdml/")]).Find(LeuzeInAml);

        Assert.Equal("gsdml/leuze/2021/gsdml-v2.41-leuze-bcl248i-20211213.xml", lookup.Found!.File);
    }

    [Fact]
    public void Same_file_in_two_directories_takes_the_first()
    {
        using var a = new TempDir();
        using var b = new TempDir();
        a.Write(LeuzeInAml, Fixtures.Read(Leuze));
        b.Write(LeuzeInAml, Fixtures.Read(Leuze));

        var lookup = GsdmlIndex.Load([(b.Path, "b"), (a.Path, "a")]).Find(LeuzeInAml);

        Assert.Equal($"a/{LeuzeInAml}", lookup.Found!.File);
        Assert.Equal(2, lookup.Candidates.Count);
    }

    [Fact]
    public void Same_name_with_different_content_is_ambiguous()
    {
        using var a = new TempDir();
        using var b = new TempDir();
        a.Write(LeuzeInAml, Fixtures.Read(Leuze));
        b.Write(LeuzeInAml, Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(Fixtures.Read(Leuze)).Replace("V1.*.*", "V2.*.*", StringComparison.Ordinal)));

        var lookup = GsdmlIndex.Load([(a.Path, "a"), (b.Path, "b")]).Find(LeuzeInAml);

        Assert.True(lookup.IsAmbiguous);
        Assert.Equal([$"a/{LeuzeInAml}", $"b/{LeuzeInAml}"], lookup.Candidates);
    }

    [Fact]
    public void Missing_directory_ends_with_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => GsdmlIndex.Load([(Fixtures.PathOf("gibt-es-nicht"), "gibt-es-nicht")]));

        Assert.Equal("GSDML_DIR_NOT_FOUND", ex.Code);
    }
}
