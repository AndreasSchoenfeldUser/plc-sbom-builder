using System.Text.Json;
using System.Text.Json.Nodes;
using PlcSbom.Core.Sbom;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Sbom;

public sealed class SbomViewTests
{
    [Fact]
    public async Task Sample_machine_namespace_components_and_dependencies()
    {
        var view = (await SampleMachine.Get()).View();

        Assert.Equal("firma", view.Namespace);
        Assert.Equal("Maschine XY", view.Primary!.Name);
        Assert.Equal(48, view.Components.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 11, 0, 0, TimeSpan.Zero), view.Timestamp);
        Assert.Equal(new SbomParty("Musterbau GmbH", "sbom@musterbau.example", null), view.Creator);
        var cpu = view.Find("plc-1-fw")!;
        Assert.True(cpu.IsDevice);
        Assert.Equal("PLC_1", cpu.Station);
        Assert.Equal("6ES7 214-1AG40-0XB0", cpu.Own("device:orderNumber"));
        Assert.True(cpu.HasDistributionHash); // from the synthetic vendor SBOM
        Assert.Equal(["firmware-projected-only"], cpu.Gaps);
        Assert.Equal(2, view.DependsOn["plc-1-fw"].Count);
        Assert.Equal("complete", view.Completeness["plc-1-fw"]);
        Assert.Equal(new SbomParty("Siemens AG", null, "https://www.siemens.com"), cpu.Supplier);
    }

    [Fact]
    public async Task Tools_carry_the_engineering_release()
    {
        var view = (await SampleMachine.Get()).View();

        Assert.Contains(new SbomTool("TIA Portal", "V17", "Siemens AG", "1700.0.4302.1"), view.Tools);
        Assert.Contains(view.Tools, t => t.Name == "PLC SBOM Builder" && t.Version == "0.1.0");
    }

    [Fact]
    public void Namespace_falls_back_to_the_station_property_and_never_is_cdx_or_bsi()
    {
        var bom = MinimalSbom.Create();
        MinimalSbom.RemoveProperty(bom["metadata"]!["component"]!.AsObject(), "plcsbom:logicalComponent");
        MinimalSbom.AddProperty(MinimalSbom.App(bom), "cdx:station", "X");

        var view = SbomView.From(bom);

        Assert.Equal("plcsbom", view.Namespace);
        Assert.Equal("PLC_1", view.Components[0].Station);
    }

    [Fact]
    public void Foreign_sbom_has_no_namespace_but_components()
    {
        var bom = MinimalSbom.Create();
        MinimalSbom.RemoveProperty(bom["metadata"]!["component"]!.AsObject(), "plcsbom:logicalComponent");
        MinimalSbom.RemoveProperty(MinimalSbom.App(bom), "plcsbom:station");

        var view = SbomView.From(bom);

        Assert.Null(view.Namespace);
        var app = Assert.Single(view.Components);
        Assert.Null(app.Station);
        Assert.Null(app.Own("station"));
        Assert.Empty(app.Gaps);
        Assert.False(app.IsDevice);
    }

    [Fact]
    public void Nested_components_are_listed_with_their_parent()
    {
        var bom = MinimalSbom.Create();
        MinimalSbom.App(bom)["components"] = new JsonArray(new JsonObject { ["bom-ref"] = "inner", ["type"] = "library", ["name"] = "Innen" });

        var view = SbomView.From(bom);

        Assert.Equal(["app", "inner"], view.Components.Select(c => c.BomRef));
        Assert.Equal("app", view.Find("inner")!.Parent!.BomRef);
    }

    [Fact]
    public void Text_that_is_no_json_object_throws()
    {
        Assert.ThrowsAny<JsonException>(() => SbomView.Parse("[]"));
        Assert.ThrowsAny<JsonException>(() => SbomView.Parse("{"));
    }
}
