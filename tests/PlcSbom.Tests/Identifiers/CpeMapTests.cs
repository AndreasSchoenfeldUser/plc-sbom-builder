using PlcSbom.Core;
using PlcSbom.Core.Identifiers;
using PlcSbom.Core.Model;

namespace PlcSbom.Tests.Identifiers;

public sealed class CpeMapTests
{
    private const string Table = """
        - vendor: Siemens AG
          orderNumber: "6ES7 511-1AK0?-0AB0"
          cpe: "cpe:2.3:o:siemens:simatic_s7-1500_cpu_1511-1_pn_firmware:{version}:*:*:*:*:*:*:*"
        """;

    private static Device Cpu(string? firmware = "V2.8", string vendor = "Siemens AG", string orderNumber = "6ES7 511-1AK02-0AB0") => new()
    {
        TypeName = "CPU 1511-1 PN",
        Vendor = new Party(vendor),
        OrderNumber = orderNumber,
        Firmware = firmware,
    };

    [Fact]
    public void Matching_row_gives_cpe_with_version_without_v()
    {
        var cpe = CpeMap.Parse(Table, "cpe-map.yml").Find(Cpu());

        Assert.Equal("cpe:2.3:o:siemens:simatic_s7-1500_cpu_1511-1_pn_firmware:2.8:*:*:*:*:*:*:*", cpe);
    }

    [Theory]
    [InlineData("6ES7511-1AK02-0AB0")]
    [InlineData("6ES7 511-1AK01-0AB0")]
    public void Order_number_matches_with_and_without_spaces(string orderNumber)
    {
        Assert.NotNull(CpeMap.Parse(Table, "cpe-map.yml").Find(Cpu(orderNumber: orderNumber)));
    }

    [Fact]
    public void No_cpe_without_firmware_version()
    {
        Assert.Null(CpeMap.Parse(Table, "cpe-map.yml").Find(Cpu(firmware: null)));
    }

    [Theory]
    [InlineData("Siemens", "6ES7 511-1AK02-0AB0")]
    [InlineData("Siemens AG", "6ES7 512-1AK02-0AB0")]
    public void Other_vendor_or_order_number_gets_no_cpe(string vendor, string orderNumber)
    {
        Assert.Null(CpeMap.Parse(Table, "cpe-map.yml").Find(Cpu(vendor: vendor, orderNumber: orderNumber)));
    }

    [Theory]
    [InlineData("V2.8", "2.8")]
    [InlineData("4.7.10", "4.7.10")]
    [InlineData("V", "V")]
    [InlineData("Vx1", "Vx1")]
    public void Version_prefix_is_removed_only_before_a_digit(string firmware, string expected)
    {
        Assert.Equal(expected, CpeMap.VersionWithoutPrefix(firmware));
    }

    [Fact]
    public void Special_characters_of_the_version_are_quoted()
    {
        Assert.Contains(":2.8\\ SP1:", CpeMap.Parse(Table, "cpe-map.yml").Find(Cpu(firmware: "V2.8 SP1")));
    }

    [Theory]
    [InlineData("- vendor: A\n  orderNumber: B\n  cpe: \"cpe:2.3:o:a:b:2.0:*:*:*:*:*:*:*\"\n", "Platzhalter {version}")]
    [InlineData("- vendor: A\n  orderNumber: B\n  cpe: \"cpe:/o:a:b:{version}\"\n", "CPE 2.3")]
    [InlineData("- vendor: A\n  cpe: \"cpe:2.3:o:a:b:{version}:*:*:*:*:*:*:*\"\n", "„orderNumber“ fehlt")]
    [InlineData("- vendor: A\n  orderNumber: B\n  cpe: \"cpe:2.3:o:a:b:{version}:*:*:*:*:*:*:*\"\n  hinweis: x\n", "Unbekannter Schlüssel „hinweis“")]
    [InlineData("vendor: A\n", "Liste von Einträgen")]
    public void Invalid_tables_are_rejected(string yaml, string message)
    {
        var ex = Assert.Throws<UsageException>(() => CpeMap.Parse(yaml, "cpe-map.yml"));

        Assert.Equal("CPE_MAP_INVALID", ex.Code);
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void Missing_table_is_cpe_map_not_found()
    {
        Assert.Equal("CPE_MAP_NOT_FOUND", Assert.Throws<UsageException>(() => CpeMap.Load("gibt-es-nicht.yml", "gibt-es-nicht.yml")).Code);
    }

    [Fact]
    public void Sample_table_is_valid()
    {
        var map = CpeMap.Load(RepoPaths.Of("samples", "maschine-xy", "kennungen", "cpe-map.yml"), "kennungen/cpe-map.yml");

        Assert.Single(map.Entries);
    }
}
