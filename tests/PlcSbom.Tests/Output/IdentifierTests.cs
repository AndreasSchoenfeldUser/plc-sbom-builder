using PlcSbom.Core.Output;

namespace PlcSbom.Tests.Output;

public sealed class IdentifierTests
{
    [Fact]
    public void Uuid5_matches_the_rfc_9562_example()
    {
        var dns = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        Assert.Equal(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"), Uuid5.Create(dns, "www.example.com"));
    }

    [Theory]
    [InlineData("PLC_1", "plc-1")]
    [InlineData("Roboterzelle", "roboterzelle")]
    [InlineData("Förderband Süd", "foerderband-sued")]
    [InlineData("Tc2_Standard", "tc2-standard")]
    [InlineData("0/1", "0-1")]
    [InlineData("___", "x")]
    public void Slug_is_lower_case_ascii(string text, string slug)
    {
        Assert.Equal(slug, BomRef.Slug(text));
    }

    [Fact]
    public void Registry_makes_refs_unique_in_call_order()
    {
        var registry = new BomRef.Registry();

        Assert.Equal("a", registry.Claim("a"));
        Assert.Equal("a-2", registry.Claim("a"));
        Assert.Equal("a-3", registry.Claim("a"));
    }

    [Theory]
    [InlineData("build/sbom-maschine.cdx.json", "build/sbom-maschine.result.json")]
    [InlineData("x.json", "x.result.json")]
    [InlineData("x.sbom", "x.sbom.result.json")]
    public void Result_file_sits_next_to_the_sbom(string sbom, string result)
    {
        Assert.Equal(result.Replace('/', Path.DirectorySeparatorChar), ResultFile.PathFor(sbom.Replace('/', Path.DirectorySeparatorChar)));
    }
}
