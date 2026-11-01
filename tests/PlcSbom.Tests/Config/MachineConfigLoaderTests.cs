using PlcSbom.Core;
using PlcSbom.Core.Adapters.TiaAml;
using PlcSbom.Core.Config;

namespace PlcSbom.Tests.Config;

public sealed class MachineConfigLoaderTests
{
    private const string Minimal = """
        machine:
          name: Maschine XY
          supplier: Musterbau GmbH
        stations:
          - name: Zelle
            adapter: twincat
            path: twincat/Zelle.tsproj
        """;

    private static MachineConfig Parse(string yaml) => MachineConfigLoader.Parse(yaml, Path.GetFullPath("machine.yml"), "machine.yml");

    private static UsageException Invalid(string yaml)
    {
        var ex = Assert.Throws<UsageException>(() => Parse(yaml));
        Assert.Equal("CONFIG_INVALID", ex.Code);
        return ex;
    }

    [Fact]
    public void Sample_machine_is_read_completely()
    {
        var config = MachineConfigLoader.Load(RepoPaths.Of("samples", "maschine-xy", "machine.yml"));

        Assert.Equal(new MachineSection("Maschine XY", "Musterbau GmbH", "sbom@musterbau.example"), config.Machine);
        Assert.Equal("firma", config.Namespace);
        Assert.Equal(["../../fixtures/gsdml"], config.Gsdml);
        Assert.Equal(["Verpackung", "PLC_1", "Roboterzelle"], config.Stations.Select(s => s.Name));
        Assert.Equal(["codesys", "tia-aml", "twincat"], config.Stations.Select(s => s.Adapter));
        Assert.Equal("v35", config.Stations[0].Source);
        var plc1 = config.Stations[1];
        Assert.Equal("PLC_1", plc1.Plc);
        Assert.Equal(["GSD device_1"], plc1.Devices);
        Assert.Equal([new DeclaredLibrary("LGF", "Siemens AG", "7.0.1")], plc1.Libraries);
        Assert.Equal("programm/PLC_1", plc1.Program);
        Assert.Equal(["sbom/lieferanten/*.cdx.json"], config.VendorSboms);
        Assert.Equal([new PartialSbomConfig("sbom/teil/hmi-web.cdx.json", "PLC_1")], config.PartialSboms);
        Assert.Equal("kennungen/cpe-map.yml", config.CpeMap);
        Assert.Equal("LicenseRef-siemens-proprietary", config.Licenses.Single(l => l.Key == "Siemens AG").Value);
        Assert.Equal("https://www.siemens.com", config.VendorContacts.Single(c => c.Key == "Siemens AG").Value);
        Assert.StartsWith("https://git.musterbau.example/", config.SourceUrl);
    }

    [Fact]
    public void Defaults_without_optional_keys()
    {
        var config = Parse(Minimal);

        Assert.Equal("plcsbom", config.Namespace);
        Assert.Null(config.Machine.Contact);
        Assert.Empty(config.VendorSboms);
        Assert.Empty(config.PartialSboms);
        Assert.Null(config.Stations[0].Program);
    }

    [Fact]
    public void Values_stay_text_as_written()
    {
        var config = Parse(Minimal.Replace("adapter: twincat", "adapter: tia-aml\n    libraries:\n      - { name: LGF, version: 1.10 }", StringComparison.Ordinal));

        Assert.Equal("1.10", config.Stations[0].Libraries[0].Version);
    }

    [Fact]
    public void Vendor_overrides_keep_their_order()
    {
        var config = Parse(Minimal + "\nvendorOverrides:\n  \"3RK*\": Siemens AG\n  \"*\": Fremd GmbH\n");

        Assert.Equal(["3RK*", "*"], config.VendorOverrides.Select(o => o.Key));
    }

    [Theory]
    [InlineData("vendorSboms: sbom/*.cdx.json", 1)]
    [InlineData("vendorSboms: [a/*.cdx.json, b/*.cdx.json]", 2)]
    public void Vendor_sboms_take_one_pattern_or_a_list(string line, int count)
    {
        Assert.Equal(count, Parse(Minimal + "\n" + line + "\n").VendorSboms.Count);
    }

    [Fact]
    public void Missing_file_is_config_not_found()
    {
        var ex = Assert.Throws<UsageException>(() => MachineConfigLoader.Load("gibt-es-nicht/machine.yml"));

        Assert.Equal("CONFIG_NOT_FOUND", ex.Code);
        Assert.Equal(ExitCodes.Usage, ex.ExitCode);
    }

    [Fact]
    public void Unknown_top_key_names_line_and_allowed_keys()
    {
        var ex = Invalid(Minimal + "\nstattions: []\n");

        Assert.Contains("machine.yml, Zeile 8:", ex.Message);
        Assert.Contains("„stattions“", ex.Message);
        Assert.Contains("vendorSboms", ex.Message);
    }

    [Fact]
    public void Unknown_station_key_is_an_error()
    {
        Assert.Contains("„pfad“", Invalid(Minimal + "\n    pfad: x\n").Message);
    }

    [Theory]
    [InlineData("    plc: PLC_1", "„plc“ gilt nicht für den Adapter twincat")]
    [InlineData("    source: v35", "„source“ gilt nicht für den Adapter twincat")]
    [InlineData("    libraries: []", "„libraries“ gilt nicht für den Adapter twincat")]
    [InlineData("    allowUpgrade: true", "„allowUpgrade“ gilt nicht für den Adapter twincat")]
    public void Keys_of_other_adapters_are_errors(string line, string message)
    {
        Assert.Contains(message, Invalid(Minimal + "\n" + line + "\n").Message);
    }

    [Fact]
    public void Codesys_needs_its_source_variant()
    {
        Assert.Contains("„source“ fehlt", Invalid(Minimal.Replace("twincat", "codesys", StringComparison.Ordinal)).Message);
        Assert.Contains("„v36“", Invalid(Minimal.Replace("adapter: twincat", "adapter: codesys\n    source: v36", StringComparison.Ordinal)).Message);
    }

    [Fact]
    public void Unknown_adapter_lists_the_known_ones()
    {
        var message = Invalid(Minimal.Replace("twincat", "step5", StringComparison.Ordinal)).Message;

        Assert.Contains("„step5“", message);
        Assert.Contains("studio5000", message);
    }

    [Fact]
    public void Station_names_are_unique()
    {
        var yaml = Minimal + "\n  - name: Zelle\n    adapter: twincat\n    path: b.tsproj\n";

        Assert.Contains("„Zelle“ kommt mehrfach vor", Invalid(yaml).Message);
    }

    [Theory]
    [InlineData("machine:\n  name: X\nstations:\n  - { name: Z, adapter: twincat, path: a }\n", "„supplier“ fehlt")]
    [InlineData("machine:\n  name: X\n  supplier: Y\n", "„stations“ fehlt")]
    [InlineData("machine:\n  name: X\n  supplier: Y\nstations: []\n", "„stations“ ist leer")]
    [InlineData("machine: X\nstations: []\n", "„machine“ muss eine Zuordnung")]
    [InlineData("", "Die Datei ist leer.")]
    public void Required_parts_are_checked(string yaml, string message)
    {
        Assert.Contains(message, Invalid(yaml).Message);
    }

    [Fact]
    public void Yaml_syntax_error_is_config_invalid()
    {
        Assert.Contains("Kein gültiges YAML", Invalid("machine: [unclosed\n").Message);
    }

    [Fact]
    public void Duplicate_keys_are_an_error()
    {
        Assert.Contains("Kein gültiges YAML", Invalid(Minimal + "\nnamespace: a\nnamespace: b\n").Message);
    }

    [Theory]
    [InlineData("cdx")]
    [InlineData("BSI")]
    [InlineData("mit leerzeichen")]
    public void Reserved_or_malformed_namespace_is_rejected(string ns)
    {
        Assert.Contains("Namespace", Invalid(Minimal + $"\nnamespace: {ns}\n").Message);
    }

    [Fact]
    public void Licence_must_be_an_spdx_identifier()
    {
        Assert.Contains("„Proprietär“", Invalid(Minimal + "\nlicenses:\n  \"Siemens AG\": Proprietär\n").Message);
    }

    [Theory]
    [InlineData("sbom@musterbau.example")]
    [InlineData("mailto:sbom@musterbau.example")]
    [InlineData("https://www.musterbau.example")]
    public void Contact_is_an_email_address_or_url(string contact)
    {
        Assert.Equal(contact, Parse(Minimal.Replace("supplier: Musterbau GmbH", $"supplier: Musterbau GmbH\n  contact: \"{contact}\"", StringComparison.Ordinal)).Machine.Contact);
    }

    [Theory]
    [InlineData("machine:\n  name: X\n  supplier: Y\n  contact: Telefon 089 123\n")]
    [InlineData("vendorContacts:\n  \"Siemens AG\": www.siemens.com\n")]
    public void Contact_without_email_or_url_is_rejected(string yaml)
    {
        var text = yaml.StartsWith("machine", StringComparison.Ordinal) ? yaml + "stations:\n  - { name: Z, adapter: twincat, path: a }\n" : Minimal + "\n" + yaml;

        Assert.Contains("erwartet eine E-Mail-Adresse oder eine URL", Invalid(text).Message);
    }

    [Theory]
    [InlineData("sbom@musterbau.example", "sbom@musterbau.example", null)]
    [InlineData("mailto:sbom@musterbau.example", "sbom@musterbau.example", null)]
    [InlineData("https://www.musterbau.example", null, "https://www.musterbau.example")]
    public void Contact_becomes_email_or_url_of_the_party(string contact, string? email, string? url)
    {
        var party = MachineConfigLoader.ToParty("Musterbau GmbH", contact);

        Assert.Equal(email, party.Email);
        Assert.Equal(url, party.Url);
    }

    [Theory]
    [InlineData("https://git.example/{path}")]
    [InlineData("https://git.example/{commit}")]
    [InlineData("git.example/{commit}/{path}")]
    public void Source_url_needs_both_placeholders(string url)
    {
        Assert.Contains("{commit} und {path}", Invalid(Minimal + $"\nsourceUrl: \"{url}\"\n").Message);
    }

    [Fact]
    public void Partial_sbom_station_must_exist()
    {
        var message = Invalid(Minimal + "\npartialSboms:\n  - path: a.cdx.json\n    station: PLC_9\n").Message;

        Assert.Contains("„PLC_9“", message);
        Assert.Contains("Vorhanden: Zelle", message);
    }

    [Fact]
    public void Allow_upgrade_takes_true_or_false()
    {
        var yaml = Minimal.Replace("adapter: twincat", "adapter: tia-portal\n    allowUpgrade: ja", StringComparison.Ordinal);

        Assert.Contains("true oder false", Invalid(yaml).Message);
    }
}
