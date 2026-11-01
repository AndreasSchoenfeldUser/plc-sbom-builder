using System.Text;
using System.Text.Json.Nodes;
using PlcSbom.Core;
using PlcSbom.Core.Build;
using PlcSbom.Core.Config;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;

namespace PlcSbom.Tests.Build;

public sealed class MachineBuilderTests : IDisposable
{
    public const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";

    private readonly string _dir = Directory.CreateTempSubdirectory("plc-sbom-build-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string SamplePath => RepoPaths.Of("samples", "maschine-xy", "machine.yml");

    /// <summary>Fixture path for machine.yml in a temporary directory: absolute, with '/', in single quotes.</summary>
    private static string F(string relative) => $"'{Fixtures.PathOf(relative).Replace('\\', '/')}'";

    private static Task<BuildOutcome> BuildSample(FakeBuildEnvironment? environment = null) =>
        MachineBuilder.BuildAsync(
            new BuildRequest { Config = MachineConfigLoader.Load(SamplePath), Release = "R2026.10", Commit = Commit, RepositoryRoot = RepoPaths.Root },
            environment ?? FakeBuildEnvironment.Reproducible());

    /// <summary>Writes machine.yml with the given stations and extra keys into the temporary directory and builds it.</summary>
    private Task<BuildOutcome> Build(string stations, string extra = "", FakeBuildEnvironment? environment = null, string? commit = Commit)
    {
        var yaml = $"""
            machine:
              name: Testmaschine
              supplier: Musterbau GmbH
              contact: sbom@musterbau.example
            {extra}
            stations:
            {stations}
            """;
        var path = Path.Combine(_dir, "machine.yml");
        File.WriteAllText(path, yaml);
        return MachineBuilder.BuildAsync(
            new BuildRequest { Config = MachineConfigLoader.Load(path), Release = "R1", Commit = commit, RepositoryRoot = _dir },
            environment ?? FakeBuildEnvironment.Reproducible());
    }

    private static string TwinCat(string name = "Zelle", string? program = null) =>
        $"  - {{ name: {name}, adapter: twincat, path: {F("twincat/aixocat-heatpump/HeatPumpTestBenchControl.tsproj")}{(program is null ? "" : $", program: {program}")} }}";

    private static string TiaV17(string devices = "[\"GSD device_1\"]") =>
        $"  - {{ name: PLC_1, adapter: tia-aml, path: {F("tia-aml/plc-commissioning-sample_device.aml")}, plc: PLC_1, devices: {devices} }}";

    private static byte[] Write(BuildOutcome outcome, string ns = "plcsbom") =>
        CycloneDxWriter.Write(outcome.Machine, new WriterOptions { Namespace = ns, Timestamp = outcome.Timestamp, ToolVersion = "0.1.0" });

    private static JsonObject Json(BuildOutcome outcome, string ns = "plcsbom") => JsonNode.Parse(Write(outcome, ns))!.AsObject();

    private static IEnumerable<string> Codes(BuildOutcome outcome) => outcome.Findings.Select(f => f.Code);

    private static Device Device(BuildOutcome outcome, Func<Device, bool> match) =>
        outcome.Machine.Stations.SelectMany(s => s.Devices).Concat(outcome.Machine.UnassignedDevices).Single(match);

    private static SoftwareItem Program(BuildOutcome outcome, string station) =>
        outcome.Machine.Stations.Single(s => s.Name == station).Software.Single(s => s.Kind == SoftwareKind.UserProgram);

    // ---- Sample machine (CLAUDE.md M9) ---------------------------------------------------------

    [Fact]
    public async Task Sample_machine_matches_golden_file()
    {
        Golden.AssertMatches("maschine-xy.cdx.json", Write(await BuildSample(), "firma"));
    }

    [Fact]
    public async Task Two_runs_give_identical_bytes()
    {
        Assert.Equal(Write(await BuildSample(), "firma"), Write(await BuildSample(), "firma"));
    }

    [Fact]
    public async Task Sample_machine_passes_the_validator_without_errors()
    {
        var report = await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(Write(await BuildSample(), "firma")));

        Assert.True(report.SchemaValid);
        Assert.DoesNotContain(report.Findings, f => f.Severity == Severity.Error);
        Assert.Equal(3, report.Stations);
    }

    [Fact]
    public async Task Sample_machine_has_three_stations_and_one_program_each()
    {
        var outcome = await BuildSample();

        Assert.Equal(["Verpackung", "PLC_1", "Roboterzelle"], outcome.Stations.Select(s => s.Name));
        Assert.Equal(["codesys", "tia-aml", "twincat"], outcome.Stations.Select(s => s.Adapter));
        Assert.All(outcome.Machine.Stations, s => Assert.Single(s.Software, i => i.Kind == SoftwareKind.UserProgram));
        Assert.Equal(1, outcome.VendorSboms);
        Assert.Equal(1, outcome.VendorSbomsUsed);
        Assert.Equal(["syft"], outcome.PartialSbomTools);
        Assert.Equal(1, outcome.GsdmlFiles);
    }

    [Fact]
    public async Task Sample_paths_are_relative_to_the_repository()
    {
        var outcome = await BuildSample();

        Assert.Equal("fixtures/codesys/v35/robin-codesys_project.xml", outcome.Machine.Stations[0].SourcePath);
        Assert.Equal("samples/maschine-xy/programm/Verpackung", Program(outcome, "Verpackung").SourceProperties["sourceCode:path"]);
        Assert.Equal("samples/maschine-xy/sbom/teil/hmi-web.cdx.json", outcome.Machine.InputSboms.Single(i => i.Kind == InputSbomKind.Partial).SourcePath);
    }

    // ---- User program (CLAUDE.md 5.6) ----------------------------------------------------------

    [Fact]
    public async Task Program_carries_release_commit_hash_and_source_url()
    {
        var program = Program(await BuildSample(), "PLC_1");

        Assert.Equal("PLC_1, Anwenderprogramm", program.Name);
        Assert.Equal("R2026.10 (git a1b2c3d)", program.Version);
        Assert.Equal("Musterbau GmbH", program.Vendor!.Name);
        Assert.Equal("sbom@musterbau.example", program.Vendor.Email);
        Assert.Equal(ValueOrigin.Declared, program.Origin);
        Assert.Equal("LicenseRef-musterbau-proprietary", program.Licensing!.Distribution);
        Assert.Equal(MachineBuilder.UserProgramTraits, program.Traits);
        Assert.Equal($"https://git.musterbau.example/maschine-xy/-/tree/{Commit}/samples/maschine-xy/programm/PLC_1", program.SourceUri);
        Assert.Equal("2", program.SourceProperties["sourceCode:files"]);
        Assert.Equal(ProgramHash.Compute(RepoPaths.Of("samples", "maschine-xy", "programm", "PLC_1")).Sha512, program.SourceSha512);
    }

    [Fact]
    public async Task Station_without_program_gets_program_source_missing()
    {
        var outcome = await Build(TwinCat());
        var program = Program(outcome, "Zelle");

        Assert.Null(program.SourceSha512);
        Assert.Contains(program.Gaps, g => g.Code == GapCatalog.ProgramSourceMissing);
        Assert.DoesNotContain(program.Gaps, g => g.Code == GapCatalog.HashUnavailable);
    }

    [Fact]
    public async Task Without_source_url_the_hash_is_written_as_property()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "prog"));
        File.WriteAllText(Path.Combine(_dir, "prog", "MAIN.st"), "PROGRAM MAIN");
        var outcome = await Build(TwinCat(program: "prog"));

        var app = Json(outcome)["components"]!.AsArray().OfType<JsonObject>().Single(c => c["bom-ref"]!.ToString() == "zelle-app");
        Assert.Null(app["externalReferences"]);
        var properties = app["properties"]!.AsArray().Select(p => (p!["name"]!.ToString(), p["value"]!.ToString())).ToList();
        Assert.Contains(("plcsbom:sourceCode:sha512", ProgramHash.Compute(Path.Combine(_dir, "prog")).Sha512), properties);
        Assert.Contains(("plcsbom:sourceCode:path", "prog"), properties);
    }

    [Fact]
    public async Task Missing_program_directory_is_exit_2()
    {
        var ex = await Assert.ThrowsAsync<UsageException>(() => Build(TwinCat(program: "gibt-es-nicht")));

        Assert.Equal("PROGRAM_NOT_FOUND", ex.Code);
    }

    // ---- Commit and timestamp (CLAUDE.md 8.4) --------------------------------------------------

    [Fact]
    public async Task Commit_comes_from_git_commit_variable()
    {
        var environment = FakeBuildEnvironment.Reproducible();
        environment.Variables["GIT_COMMIT"] = "ABCDEF1234567";

        var outcome = await Build(TwinCat(), environment: environment, commit: null);

        Assert.Equal("abcdef1234567", outcome.Commit);
        Assert.Equal("R1 (git abcdef1)", Program(outcome, "Zelle").Version);
        Assert.Empty(environment.GitCalls);
    }

    [Fact]
    public async Task Commit_and_timestamp_come_from_git()
    {
        var environment = new FakeBuildEnvironment();
        environment.GitAnswers["rev-parse HEAD"] = Commit;
        environment.GitAnswers[$"log -1 --format=%ct {Commit}"] = "1792234800";

        var outcome = await Build(TwinCat(), environment: environment, commit: null);

        Assert.Equal(Commit, outcome.Commit);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792234800), outcome.Timestamp);
        Assert.Empty(outcome.Findings);
    }

    [Fact]
    public async Task Unknown_commit_warns_and_programs_carry_only_the_release()
    {
        var outcome = await Build(TwinCat(), commit: null);

        Assert.Null(outcome.Commit);
        Assert.Equal("R1", Program(outcome, "Zelle").Version);
        Assert.Contains("COMMIT_UNKNOWN", Codes(outcome));
        Assert.All(outcome.Findings, f => Assert.Equal(Severity.Warning, f.Severity));
    }

    [Fact]
    public async Task Timestamp_from_clock_warns_and_drops_fractions()
    {
        var outcome = await Build(TwinCat(), environment: new FakeBuildEnvironment());

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 9, 30, 15, TimeSpan.Zero), outcome.Timestamp);
        Assert.Contains("TIMESTAMP_FROM_CLOCK", Codes(outcome));
    }

    [Theory]
    [InlineData("xyz123456", "COMMIT_INVALID")]
    [InlineData("abc12", "COMMIT_INVALID")]
    public async Task Invalid_commit_is_exit_2(string commit, string code)
    {
        Assert.Equal(code, (await Assert.ThrowsAsync<UsageException>(() => Build(TwinCat(), commit: commit))).Code);
    }

    [Fact]
    public async Task Invalid_source_date_epoch_is_exit_2()
    {
        var environment = new FakeBuildEnvironment();
        environment.Variables["SOURCE_DATE_EPOCH"] = "gestern";

        Assert.Equal("SOURCE_DATE_EPOCH_INVALID", (await Assert.ThrowsAsync<UsageException>(() => Build(TwinCat(), environment: environment))).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("R 1")]
    public async Task Release_must_be_one_word(string release)
    {
        var path = Path.Combine(_dir, "machine.yml");
        File.WriteAllText(path, $"machine: {{ name: X, supplier: Y }}\nstations:\n{TwinCat()}\n");
        var request = new BuildRequest { Config = MachineConfigLoader.Load(path), Release = release, Commit = Commit };

        Assert.Equal("RELEASE_INVALID", (await Assert.ThrowsAsync<UsageException>(() => MachineBuilder.BuildAsync(request, FakeBuildEnvironment.Reproducible()))).Code);
    }

    // ---- Declarations from machine.yml ---------------------------------------------------------

    [Fact]
    public async Task Vendor_override_by_order_number_replaces_supplier_unknown()
    {
        var stations = $"  - {{ name: Linie, adapter: studio5000, path: {F("studio5000/module-baum-synthetisch.L5X")} }}";

        var outcome = await Build(stations, "vendorOverrides:\n  \"WG-*\": Wiegetechnik GmbH");

        var scale = Device(outcome, d => d.OrderNumber == "WG-100-EIP");
        Assert.Equal("Wiegetechnik GmbH", scale.Vendor!.Name);
        Assert.Equal(ValueOrigin.Declared, scale.VendorOrigin);
        Assert.DoesNotContain(scale.Gaps, g => g.Code == GapCatalog.SupplierUnknown);
    }

    [Fact]
    public async Task Vendor_override_matches_the_type_name_of_a_device_without_order_number()
    {
        var outcome = await Build(TwinCat(), "vendorOverrides:\n  \"EL3204*\": Beispiel GmbH");

        Assert.All(outcome.Machine.Stations[0].Devices.Where(d => d.TypeName == "EL3204-0200"), d => Assert.Equal("Beispiel GmbH", d.Vendor!.Name));
        Assert.Equal("Beckhoff Automation GmbH & Co. KG", Device(outcome, d => d.TypeName == "EK1100").Vendor!.Name);
    }

    [Fact]
    public async Task Passive_declaration_takes_a_terminal_out_of_the_sbom()
    {
        var outcome = await Build(TwinCat(), "passiveOrderNumbers: [\"EL2622\"]");

        var terminal = Device(outcome, d => d.TypeName == "EL2622");
        Assert.True(terminal.IsPassive);
        Assert.Equal("declared", terminal.SourceProperties["device:passive"]);
        Assert.DoesNotContain(Json(outcome)["components"]!.AsArray(), c => c!["name"]!.ToString() == "EL2622, Firmware");
    }

    [Fact]
    public async Task Controller_and_devices_with_firmware_are_never_declared_passive()
    {
        var stations = $"""
              - {"{"} name: Verpackung, adapter: codesys, source: v35, path: {F("codesys/v35/robin-codesys_project.xml")} {"}"}
            {TiaV17()}
            """;
        var outcome = await Build(stations, "passiveOrderNumbers: [\"*\"]");

        Assert.False(Device(outcome, d => d.IsController && d.TypeName.StartsWith("Gerät", StringComparison.Ordinal)).IsPassive);
        Assert.False(Device(outcome, d => d.DeviceName == "PLC_1").IsPassive);
    }

    [Fact]
    public async Task Passive_declaration_reaches_the_tia_adapter()
    {
        var stations = $"""
              - {"{"} name: Linie 1, adapter: tia-aml, path: {F("tia-aml/zwei-cpus-synthetisch.aml")}, plc: PLC_1 {"}"}
              - {"{"} name: Linie 2, adapter: tia-aml, path: {F("tia-aml/zwei-cpus-synthetisch.aml")}, plc: PLC_2 {"}"}
            """;
        var outcome = await Build(stations, "passiveOrderNumbers: [\"6EP1*\"]");

        Assert.Equal(["Linie 1", "Linie 2"], outcome.Machine.Stations.Select(s => s.Name));
        var pm = Device(outcome, d => d.OrderNumber == "6EP1332-4BA00");
        Assert.True(pm.IsPassive);
        Assert.Equal("declared", pm.SourceProperties["device:passive"]);
    }

    [Fact]
    public async Task Contacts_and_licences_close_the_vendor_gaps()
    {
        var extra = """
            vendorContacts:
              "Beckhoff Automation GmbH & Co. KG": https://www.beckhoff.com
            licenses:
              "Beckhoff Automation GmbH & Co. KG": LicenseRef-beckhoff-proprietary
              "Musterbau GmbH": LicenseRef-musterbau-proprietary
            """;
        var outcome = await Build(TwinCat(), extra);

        var coupler = Device(outcome, d => d.TypeName == "EK1100");
        Assert.Equal("https://www.beckhoff.com", coupler.Vendor!.Url);
        Assert.Equal("LicenseRef-beckhoff-proprietary", coupler.FirmwareLicensing!.Distribution);
        Assert.DoesNotContain(coupler.Gaps, g => g.Code is GapCatalog.CreatorContactUnknown or GapCatalog.LicenseUnknown);
        Assert.Equal("LicenseRef-musterbau-proprietary", outcome.Machine.Licensing!.Distribution);
        Assert.DoesNotContain(outcome.Machine.Gaps, g => g.Code == GapCatalog.LicenseUnknown);
        // Libraries name the vendor "Beckhoff Automation GmbH" without "& Co. KG": no contact for them.
        Assert.Contains(outcome.Machine.Stations[0].Software.Single(s => s.Name == "Tc2_Standard").Gaps, g => g.Code == GapCatalog.CreatorContactUnknown);
    }

    [Fact]
    public async Task Cpe_comes_from_the_table_only()
    {
        var map = Path.Combine(_dir, "cpe-map.yml");
        File.WriteAllText(map, "- vendor: Siemens AG\n  orderNumber: \"6ES7 214-1AG40-0XB0\"\n  cpe: \"cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:{version}:*:*:*:*:*:*:*\"\n");

        var outcome = await Build(TiaV17(), $"gsdml: [{F("gsdml")}]\ncpeMap: cpe-map.yml");

        Assert.Equal("cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:4.5:*:*:*:*:*:*:*", Device(outcome, d => d.DeviceName == "PLC_1").Cpe);
        Assert.Null(Device(outcome, d => d.TypeName == "BCL248i").Cpe);
    }

    // ---- Vendor and partial SBOMs (CLAUDE.md 5.7) ------------------------------------------------

    private string VendorSbom(string name, string? orderNumber = "6ES7 214-1AG40-0XB0", string version = "V4.5", string? cpe = null, string vendor = "Siemens AG")
    {
        var properties = orderNumber is null ? "[]" : $$"""[ { "name": "x:device:orderNumber", "value": "{{orderNumber}}" } ]""";
        var cpeField = cpe is null ? "" : $"\"cpe\": \"{cpe}\",";
        Directory.CreateDirectory(Path.Combine(_dir, "sbom"));
        File.WriteAllText(Path.Combine(_dir, "sbom", name), $$"""
            { "bomFormat": "CycloneDX", "specVersion": "1.6",
              "metadata": { "component": { "bom-ref": "fw", "type": "firmware", "supplier": { "name": "{{vendor}}" },
                                           "name": "CPU", "version": "{{version}}", {{cpeField}} "properties": {{properties}} } },
              "components": [ { "bom-ref": "lib", "type": "library", "name": "lib", "version": "1" } ],
              "dependencies": [ { "ref": "fw", "dependsOn": [ "lib" ] } ] }
            """);
        return name;
    }

    [Fact]
    public async Task Vendor_sbom_hangs_under_the_firmware_it_describes()
    {
        VendorSbom("cpu.cdx.json");

        var outcome = await Build(TiaV17(), "vendorSboms: sbom/*.cdx.json");

        var cpu = Device(outcome, d => d.DeviceName == "PLC_1");
        Assert.Equal(["vendor-cpu"], cpu.VendorSboms);
        Assert.DoesNotContain(cpu.Gaps, g => g.Code == GapCatalog.VendorSbomMissing);
        var dependencies = Json(outcome)["dependencies"]!.AsArray().OfType<JsonObject>().ToDictionary(d => d["ref"]!.ToString(), d => d["dependsOn"]!.AsArray().Select(x => x!.ToString()).ToList());
        // The root of the vendor SBOM is the firmware itself: its parts hang directly under the firmware.
        Assert.Equal(["vendor-cpu/lib"], dependencies["plc-1-fw"]);
        Assert.False(dependencies.ContainsKey("vendor-cpu/fw"));
        Assert.DoesNotContain("vendor-cpu/lib", dependencies["testmaschine"]);
        // This vendor SBOM states no file: the firmware keeps hash-unavailable and filename-unavailable.
        Assert.Contains(cpu.Gaps, g => g.Code == GapCatalog.HashUnavailable);
    }

    [Fact]
    public async Task Vendor_sbom_gives_the_firmware_its_file_name_and_hash()
    {
        var outcome = await BuildSample();

        var cpu = Device(outcome, d => d.DeviceName == "PLC_1");
        Assert.Equal("cpu1214c-v4.5.upd", cpu.FirmwareFileName);
        Assert.Equal("https://www.example.com/synthetisch/cpu1214c-v4.5.upd", cpu.FirmwareUri);
        Assert.Equal(128, cpu.FirmwareSha512!.Length);
        Assert.DoesNotContain(cpu.Gaps, g => g.Code is GapCatalog.HashUnavailable or GapCatalog.FilenameUnavailable or GapCatalog.VendorSbomMissing);
        var json = Json(outcome, "firma");
        Assert.DoesNotContain(json["components"]!.AsArray(), c => c!["bom-ref"]!.ToString().EndsWith("/fw", StringComparison.Ordinal));
        var composition = json["compositions"]!.AsArray().Single(c => c!["aggregate"]!.ToString() == "complete")!;
        Assert.Contains("plc-1-fw", composition["dependencies"]!.AsArray().Select(x => x!.ToString()));
    }

    [Theory]
    [InlineData("V4.6", null, "VENDOR_SBOM_UNUSED")]
    [InlineData("4.5", null, null)]
    [InlineData("V4.5", "cpe:2.3:o:siemens:andere:4.5:*:*:*:*:*:*:*", null)]
    public async Task Vendor_sbom_matches_on_vendor_order_number_and_version(string version, string? cpe, string? warning)
    {
        VendorSbom("cpu.cdx.json", version: version, cpe: cpe);

        var outcome = await Build(TiaV17(), "vendorSboms: sbom/*.cdx.json");

        Assert.Equal(warning is null ? 1 : 0, outcome.Machine.InputSboms.Count);
        Assert.Equal(warning is null, Codes(outcome).All(c => c != "VENDOR_SBOM_UNUSED"));
    }

    [Fact]
    public async Task Vendor_sbom_matches_on_cpe_without_order_number()
    {
        var map = Path.Combine(_dir, "cpe-map.yml");
        File.WriteAllText(map, "- vendor: Siemens AG\n  orderNumber: \"6ES7 214*\"\n  cpe: \"cpe:2.3:o:siemens:s7-1200:{version}:*:*:*:*:*:*:*\"\n");
        VendorSbom("cpu.cdx.json", orderNumber: null, vendor: "Jemand anderes", cpe: "cpe:2.3:o:siemens:s7-1200:4.5:*:*:*:*:*:*:*");

        var outcome = await Build(TiaV17(), "vendorSboms: sbom/*.cdx.json\ncpeMap: cpe-map.yml");

        Assert.Equal(["vendor-cpu"], Device(outcome, d => d.DeviceName == "PLC_1").VendorSboms);
    }

    [Fact]
    public async Task Vendor_sbom_without_key_is_not_merged()
    {
        VendorSbom("cpu.cdx.json", orderNumber: null);

        var outcome = await Build(TiaV17(), "vendorSboms: sbom/*.cdx.json");

        Assert.Contains("VENDOR_SBOM_NO_KEY", Codes(outcome));
        Assert.Empty(outcome.Machine.InputSboms);
    }

    [Fact]
    public async Task Unreadable_input_sbom_is_an_error()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "sbom"));
        File.WriteAllText(Path.Combine(_dir, "sbom", "kaputt.cdx.json"), "{ nicht json");

        var outcome = await Build(TiaV17(), "vendorSboms: sbom/*.cdx.json");

        var finding = Assert.Single(outcome.Findings, f => f.Code == "INPUT_SBOM_INVALID");
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Contains("sbom/kaputt.cdx.json", finding.Message);
    }

    [Fact]
    public async Task Pattern_without_files_warns()
    {
        var outcome = await Build(TiaV17(), "vendorSboms: [sbom/*.cdx.json]");

        Assert.Contains("VENDOR_SBOMS_NOT_FOUND", Codes(outcome));
        Assert.Equal(0, outcome.VendorSboms);
    }

    [Fact]
    public async Task Partial_sbom_without_station_hangs_under_the_machine()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "teil"));
        File.Copy(RepoPaths.Of("samples", "maschine-xy", "sbom", "teil", "hmi-web.cdx.json"), Path.Combine(_dir, "teil", "web.cdx.json"));

        var outcome = await Build(TwinCat(), "partialSboms:\n  - path: teil/web.cdx.json");

        var json = Json(outcome);
        var machine = json["dependencies"]!.AsArray().OfType<JsonObject>().Single(d => d["ref"]!.ToString() == "testmaschine");
        Assert.Contains("partial-web/af63bd4c8601b7f1", machine["dependsOn"]!.AsArray().Select(x => x!.ToString()));
        var root = json["components"]!.AsArray().OfType<JsonObject>().Single(c => c["bom-ref"]!.ToString() == "partial-web/af63bd4c8601b7f1");
        Assert.DoesNotContain(root["properties"]!.AsArray(), p => p!["name"]!.ToString() == "plcsbom:station");
    }

    [Fact]
    public async Task Missing_partial_sbom_is_exit_2()
    {
        Assert.Equal("PARTIAL_SBOM_NOT_FOUND", (await Assert.ThrowsAsync<UsageException>(() => Build(TwinCat(), "partialSboms:\n  - path: fehlt.cdx.json"))).Code);
    }

    // ---- Adapters not built yet ----------------------------------------------------------------

    [Theory]
    [InlineData("  - { name: A, adapter: codesys, source: v23, path: a.pro }", "M3")]
    [InlineData("  - { name: A, adapter: codesys, source: c4, path: a }", "M4")]
    [InlineData("  - { name: A, adapter: tia-portal, path: a.zap21 }", "M14")]
    public async Task Sources_not_built_yet_name_their_milestone(string station, string milestone)
    {
        var ex = await Assert.ThrowsAsync<UsageException>(() => Build(station));

        Assert.Equal("NOT_IMPLEMENTED", ex.Code);
        Assert.Contains($"Meilenstein {milestone}.", ex.Message);
    }

    [Fact]
    public async Task Missing_station_source_is_exit_2()
    {
        var ex = await Assert.ThrowsAsync<UsageException>(() => Build("  - { name: A, adapter: twincat, path: fehlt.tsproj }"));

        Assert.Equal("SOURCE_NOT_FOUND", ex.Code);
    }

    [Fact]
    public void Repository_root_is_the_directory_with_git()
    {
        var sub = Path.Combine(_dir, "a", "b");
        Directory.CreateDirectory(sub);
        Directory.CreateDirectory(Path.Combine(_dir, ".git"));

        Assert.Equal(_dir, MachineBuilder.FindRepositoryRoot(sub));
    }

    [Fact]
    public void Outside_a_repository_the_root_is_the_start_directory()
    {
        var start = Path.GetPathRoot(_dir)!;

        Assert.Equal(start, MachineBuilder.FindRepositoryRoot(start));
    }
}
