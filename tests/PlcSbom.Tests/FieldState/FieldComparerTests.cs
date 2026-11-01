using System.Text;
using System.Text.Json.Nodes;
using PlcSbom.Core;
using PlcSbom.Core.FieldState;
using PlcSbom.Core.Sbom;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.FieldState;

public sealed class FieldComparerTests
{
    private static async Task<Comparison> CompareSample() => FieldComparer.Compare((await SampleMachine.Get()).View(), SampleMachine.Field());

    private static AsFoundState Field(params string[] devices) =>
        FieldStateReader.Parse(Encoding.UTF8.GetBytes($$"""
            { "machine": "Maschine XY", "serialNumber": "9", "date": "2026-10-22", "devices": [ {{string.Join(", ", devices)}} ] }
            """), "f.json");

    /// <summary>SBOM with firmware components of the given (position, order number, version), station PLC_1.</summary>
    private static SbomView Sbom(params (string? Position, string? OrderNumber, string? Version)[] devices)
    {
        var bom = MinimalSbom.Create();
        var components = bom["components"]!.AsArray();
        var i = 0;
        foreach (var (position, orderNumber, version) in devices)
        {
            var properties = new JsonArray(new JsonObject { ["name"] = "plcsbom:station", ["value"] = "PLC_1" }, new JsonObject { ["name"] = "plcsbom:device:type", ["value"] = $"Typ {i}" });
            if (position is not null)
            {
                properties.Add(new JsonObject { ["name"] = "plcsbom:device:position", ["value"] = position });
            }
            if (orderNumber is not null)
            {
                properties.Add(new JsonObject { ["name"] = "plcsbom:device:orderNumber", ["value"] = orderNumber });
            }
            var component = new JsonObject { ["bom-ref"] = $"fw-{i++}", ["type"] = "firmware", ["name"] = "Firmware" };
            if (version is not null)
            {
                component["version"] = version;
            }
            component["properties"] = properties;
            components.Add(component);
        }
        return SbomView.From(bom);
    }

    [Fact]
    public async Task Sample_machine_against_field_file_1042()
    {
        var comparison = await CompareSample();

        Assert.Equal(13, comparison.Checked);
        Assert.Equal(2, comparison.Count(ComparisonStatus.Equal));
        Assert.Equal(1, comparison.Count(ComparisonStatus.Different));
        Assert.Equal(10, comparison.Count(ComparisonStatus.ProjectOnly));
        Assert.Equal(1, comparison.Count(ComparisonStatus.FieldOnly));
        Assert.Equal(1, comparison.Count(ComparisonStatus.Passive));
        Assert.Equal(12, comparison.Deviations);
        var cpu = comparison.Entries.Single(e => e.Sbom?.BomRef == "plc-1-fw");
        Assert.Equal(ComparisonStatus.Different, cpu.Status);
        Assert.Equal("position", cpu.MatchedBy);
        Assert.Equal(["Firmware im Feld V4.6.1, in der SBOM V4.5."], cpu.Notes);
        var scanner = comparison.Entries.Single(e => e.Sbom?.BomRef == "plc-1-fw-gsd-device-1-0-0");
        Assert.Equal(ComparisonStatus.Equal, scanner.Status); // 501xxxxx matches 50132148
        Assert.Contains("nur im Feld bekannt", scanner.Notes[0]);
        Assert.Equal("6GK5 208-0BA00-2AC2", comparison.Entries.Single(e => e.Status == ComparisonStatus.FieldOnly).Field!.OrderNumber);
    }

    [Fact]
    public void Position_wins_over_order_number()
    {
        var sbom = Sbom(("R/0/1", "6ES7 511-1AK02-0AB0", "V2.8"), ("R/0/2", "6ES7 511-1AK02-0AB0", "V2.9"));

        var comparison = FieldComparer.Compare(sbom, Field("""{ "position": "R/0/2", "orderNumber": "6ES7 511-1AK02-0AB0", "firmware": "V2.9" }"""));

        var entry = comparison.Entries.Single(e => e.Field is not null);
        Assert.Equal("fw-1", entry.Sbom!.BomRef);
        Assert.Equal(ComparisonStatus.Equal, entry.Status);
    }

    [Fact]
    public void Unknown_position_falls_back_to_the_order_number_without_spaces()
    {
        var sbom = Sbom(("R/0/1", "6ES7 511-1AK02-0AB0", "V2.8"));

        var entry = FieldComparer.Compare(sbom, Field("""{ "position": "anders", "orderNumber": "6es7511-1ak02-0ab0", "firmware": "V2.8.3" }""")).Entries.Single();

        Assert.Equal("orderNumber", entry.MatchedBy);
        Assert.Equal(ComparisonStatus.Equal, entry.Status);
        Assert.Equal(["Firmware im Feld V2.8.3 genauer als in der SBOM (V2.8)."], entry.Notes);
    }

    [Fact]
    public void Among_equal_order_numbers_the_same_firmware_is_matched_first()
    {
        var sbom = Sbom((null, "A-1", "V1.0"), (null, "A-1", "V2.0"));

        var comparison = FieldComparer.Compare(sbom, Field("""{ "orderNumber": "A-1", "firmware": "V2.0" }""", """{ "orderNumber": "A-1", "firmware": "V1.0" }"""));

        Assert.All(comparison.Entries, e => Assert.Equal(ComparisonStatus.Equal, e.Status));
    }

    [Fact]
    public void Different_order_number_at_the_same_position_is_a_deviation()
    {
        var entry = FieldComparer.Compare(Sbom(("R/0/1", "6ES7 511-1AK02-0AB0", "V2.8")), Field("""{ "position": "R/0/1", "orderNumber": "6ES7 511-1AL03-0AB0", "firmware": "V2.8" }""")).Entries.Single();

        Assert.Equal(ComparisonStatus.Different, entry.Status);
        Assert.Contains("Bestellnummer im Feld 6ES7 511-1AL03-0AB0", entry.Notes[0]);
    }

    [Fact]
    public void Web_api_configuration_in_the_cpu_is_compared_too()
    {
        var field = FieldStateReader.Parse(FieldStateReaderTests.WebApiFile(), "f.json");

        var same = FieldComparer.Compare(Sbom((null, "6ES7 518-3AT10-0AB0", "V4.1")), field).Entries.Single();
        var older = FieldComparer.Compare(Sbom((null, "6ES7 518-3AT10-0AB0", "V4.0")), field).Entries.Single();

        Assert.Equal(ComparisonStatus.Equal, same.Status);
        Assert.Equal(ComparisonStatus.Different, older.Status);
        Assert.Contains(older.Notes, n => n.Contains("In der CPU projektiert ist Firmware V4.1.0, nicht V4.0", StringComparison.Ordinal));
    }

    [Fact]
    public void Field_device_with_firmware_zero_and_no_match_is_passive_not_a_deviation()
    {
        var comparison = FieldComparer.Compare(Sbom(), Field("""{ "orderNumber": "6ES7 193-6BP00-0DA0", "firmware": "V0.0" }"""));

        Assert.Equal(ComparisonStatus.Passive, comparison.Entries.Single().Status);
        Assert.Equal(0, comparison.Deviations);
    }

    [Fact]
    public void Field_device_in_an_unknown_station_is_field_only_with_a_note()
    {
        var entry = FieldComparer.Compare(Sbom((null, "A", "V1")), Field("""{ "station": "PLC_9", "orderNumber": "A", "firmware": "V1" }""")).Entries.Single(e => e.Field is not null);

        Assert.Equal(ComparisonStatus.FieldOnly, entry.Status);
        Assert.Contains("Die Station PLC_9 gibt es in der SBOM nicht.", entry.Notes);
    }

    [Fact]
    public void Other_machine_is_exit_2()
    {
        var field = FieldStateReader.Parse(Encoding.UTF8.GetBytes("""{ "machine": "Andere", "serialNumber": "1", "date": "2026-10-22", "devices": [] }"""), "f.json");

        var ex = Assert.Throws<UsageException>(() => FieldComparer.Compare(Sbom(), field));

        Assert.Equal("FIELD_MACHINE_MISMATCH", ex.Code);
    }

    [Fact]
    public void Sbom_without_plc_sbom_properties_is_exit_2()
    {
        var bom = MinimalSbom.Create();
        MinimalSbom.RemoveProperty(bom["metadata"]!["component"]!.AsObject(), "plcsbom:logicalComponent");
        MinimalSbom.RemoveProperty(MinimalSbom.App(bom), "plcsbom:station");

        var ex = Assert.Throws<UsageException>(() => FieldComparer.Compare(SbomView.From(bom), Field()));

        Assert.Equal("SBOM_NOT_PLC_SBOM", ex.Code);
    }

    [Theory]
    [InlineData("V2.8", "V2.8", VersionMatch.Equal)]
    [InlineData("V2.8", "2.8", VersionMatch.Equal)]
    [InlineData("4.7.10", "V04.07.10", VersionMatch.Equal)]
    [InlineData("V2.8", "V2.8.3", VersionMatch.Refined)]
    [InlineData("V2.8.3", "V2.8", VersionMatch.Refined)]
    [InlineData("V2.8", "V2.9", VersionMatch.Different)]
    [InlineData("V2.8", "V2.80", VersionMatch.Different)]
    [InlineData(null, "V1", VersionMatch.OnlyField)]
    [InlineData("V1", null, VersionMatch.OnlySbom)]
    [InlineData(null, null, VersionMatch.None)]
    public void Firmware_versions(string? sbom, string? field, VersionMatch expected)
    {
        Assert.Equal(expected, FirmwareVersions.Compare(sbom, field));
    }

    [Theory]
    [InlineData("V0.0", true)]
    [InlineData("0.0.0", true)]
    [InlineData("V0.1", false)]
    [InlineData(null, false)]
    public void Firmware_zero(string? version, bool zero)
    {
        Assert.Equal(zero, FirmwareVersions.IsZero(version));
    }
}
