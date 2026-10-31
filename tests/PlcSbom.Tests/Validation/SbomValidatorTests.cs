using System.Text.Json.Nodes;
using PlcSbom.Core;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Samples;
using static PlcSbom.Tests.Samples.MinimalSbom;

namespace PlcSbom.Tests.Validation;

/// <summary>Rules of the validator, one change at a time against <see cref="MinimalSbom"/>.</summary>
public sealed class SbomValidatorTests
{
    private static Task<ValidationReport> Validate(JsonObject bom) => SbomValidator.ValidateAsync(bom.ToJsonString());

    private static List<(Severity Severity, string Code)> Codes(ValidationReport report) =>
        report.Findings.Select(f => (f.Severity, f.Code)).ToList();

    private static List<(Severity Severity, string Code)> WithoutLogical(ValidationReport report) =>
        Codes(report).Where(c => c.Code != "LOGICAL_COMPONENT").ToList();

    // ---- Baseline and format ------------------------------------------------------------------

    [Fact]
    public async Task Minimal_sbom_passes_with_only_the_logical_component_note()
    {
        var report = await Validate(Create());

        Assert.True(report.SchemaValid);
        Assert.Equal([(Severity.Note, "LOGICAL_COMPONENT")], Codes(report));
        Assert.Equal(ExitCodes.Ok, report.ExitCode(releaseBuild: false));
        Assert.Equal(ExitCodes.Ok, report.ExitCode(releaseBuild: true));
        Assert.Equal(2, report.Components);
        Assert.Equal(1, report.Stations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"bomFormat\": ")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"a\": 1, }")]
    public async Task Invalid_json_is_an_error(string text)
    {
        var report = await SbomValidator.ValidateAsync(text);

        Assert.False(report.SchemaValid);
        Assert.Equal([(Severity.Error, "JSON_INVALID")], Codes(report));
        Assert.Equal(ExitCodes.Finding, report.ExitCode(releaseBuild: false));
    }

    [Fact]
    public async Task Other_formats_are_not_cyclonedx()
    {
        var bom = Create();
        bom["bomFormat"] = "SPDX";

        var report = await Validate(bom);

        Assert.Contains((Severity.Error, "NOT_CYCLONEDX"), Codes(report));
        Assert.False(report.SchemaValid);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1.4")]
    [InlineData("")]
    public async Task Versions_before_1_6_do_not_meet_the_tr(string version)
    {
        var bom = Create();
        bom["specVersion"] = version;

        var report = await Validate(bom);

        Assert.Contains((Severity.Error, "SPEC_VERSION_UNSUPPORTED"), Codes(report));
    }

    [Fact]
    public async Task Newer_version_skips_the_schema_with_a_warning_but_checks_the_rules()
    {
        var bom = Create();
        bom["specVersion"] = "1.7";
        bom["metadata"]!.AsObject().Remove("timestamp");

        var report = await Validate(bom);

        Assert.False(report.SchemaValid);
        Assert.Contains((Severity.Warning, "SCHEMA_NOT_EMBEDDED"), Codes(report));
        Assert.Contains((Severity.Error, "TIMESTAMP_MISSING"), Codes(report));
    }

    [Fact]
    public async Task Newer_version_alone_fails_only_the_release_build()
    {
        var bom = Create();
        bom["specVersion"] = "1.7";

        var report = await Validate(bom);

        Assert.Equal(ExitCodes.Ok, report.ExitCode(releaseBuild: false));
        Assert.Equal(ExitCodes.Finding, report.ExitCode(releaseBuild: true));
    }

    [Fact]
    public async Task Schema_violation_is_an_error_with_its_path()
    {
        var bom = Create();
        App(bom)["type"] = "hardware";

        var report = await Validate(bom);

        Assert.False(report.SchemaValid);
        var finding = Assert.Single(report.Findings, f => f.Code == "SCHEMA_INVALID");
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Contains("components", finding.Message);
    }

    [Fact]
    public async Task Malformed_sha512_violates_the_schema()
    {
        var bom = Create();
        App(bom)["externalReferences"]![0]!["hashes"]![0]!["content"] = "abc";

        var report = await Validate(bom);

        Assert.Contains((Severity.Error, "SCHEMA_INVALID"), Codes(report));
    }

    // ---- Document level (TR 5.2.1, 3.1) -------------------------------------------------------

    [Fact]
    public async Task Vulnerabilities_are_forbidden()
    {
        var bom = Create();
        bom["vulnerabilities"] = new JsonArray(new JsonObject { ["id"] = "CVE-2026-0001" });

        var report = await Validate(bom);

        Assert.Contains((Severity.Error, "VULNERABILITIES_PRESENT"), Codes(report));
    }

    [Fact]
    public async Task Empty_vulnerability_list_is_allowed()
    {
        var bom = Create();
        bom["vulnerabilities"] = new JsonArray();

        var report = await Validate(bom);

        Assert.DoesNotContain(report.Findings, f => f.Code == "VULNERABILITIES_PRESENT");
    }

    [Fact]
    public async Task Timestamp_is_required()
    {
        var bom = Create();
        bom["metadata"]!.AsObject().Remove("timestamp");

        Assert.Contains((Severity.Error, "TIMESTAMP_MISSING"), Codes(await Validate(bom)));
    }

    [Fact]
    public async Task Sbom_creator_needs_email_or_url()
    {
        var bom = Create();
        bom["metadata"]!["manufacturer"] = new JsonObject { ["name"] = "Musterbau GmbH" };

        Assert.Contains((Severity.Error, "SBOM_CREATOR_MISSING"), Codes(await Validate(bom)));
    }

    [Fact]
    public async Task Sbom_creator_with_url_is_enough()
    {
        var bom = Create();
        bom["metadata"]!["manufacturer"] = new JsonObject { ["name"] = "Musterbau GmbH", ["url"] = new JsonArray("https://musterbau.example") };

        Assert.DoesNotContain((await Validate(bom)).Findings, f => f.Code == "SBOM_CREATOR_MISSING");
    }

    [Fact]
    public async Task Primary_component_is_required()
    {
        var bom = Create();
        bom["metadata"]!.AsObject().Remove("component");

        Assert.Contains((Severity.Error, "PRIMARY_COMPONENT_MISSING"), Codes(await Validate(bom)));
    }

    // ---- Component fields (TR 5.2.2) ----------------------------------------------------------

    public static TheoryData<string, string> MissingFields => new()
    {
        { "manufacturer", "CREATOR_MISSING" },
        { "version", "VERSION_MISSING" },
        { "licenses", "DISTRIBUTION_LICENCE_MISSING" },
        { "externalReferences", "HASH_MISSING" },
    };

    [Theory]
    [MemberData(nameof(MissingFields))]
    public async Task Missing_component_field_without_gap_is_an_error(string field, string code)
    {
        var bom = Create();
        App(bom).Remove(field);

        var report = await Validate(bom);

        var finding = Assert.Single(report.Findings, f => f.Code == code);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal(AppRef, finding.ComponentRef);
        Assert.Contains("Kein Lückenvermerk", finding.Message);
    }

    [Theory]
    [InlineData("bsi:component:filename", "FILENAME_MISSING")]
    [InlineData("bsi:component:executable", "EXECUTABLE_PROPERTY_MISSING")]
    [InlineData("bsi:component:archive", "ARCHIVE_PROPERTY_MISSING")]
    [InlineData("bsi:component:structured", "STRUCTURED_PROPERTY_MISSING")]
    public async Task Missing_bsi_property_without_gap_is_an_error(string property, string code)
    {
        var bom = Create();
        RemoveProperty(App(bom), property);

        Assert.Equal([(Severity.Error, code)], WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Component_creator_without_contact_is_missing()
    {
        var bom = Create();
        App(bom)["manufacturer"] = new JsonObject { ["name"] = "Musterbau GmbH" };

        Assert.Equal([(Severity.Error, "CREATOR_MISSING")], WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Declared_licence_is_not_a_distribution_licence()
    {
        var bom = Create();
        App(bom)["licenses"]![0]!["license"]!["acknowledgement"] = "declared";

        Assert.Equal([(Severity.Error, "DISTRIBUTION_LICENCE_MISSING")], WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Concluded_licence_expression_is_a_distribution_licence()
    {
        var bom = Create();
        App(bom)["licenses"] = new JsonArray(new JsonObject { ["expression"] = "MIT OR Apache-2.0", ["acknowledgement"] = "concluded" });

        Assert.Empty(WithoutLogical(await Validate(bom)));
    }

    [Theory]
    [InlineData("LicenseRef-siemens-proprietary")]
    [InlineData("(MIT OR Apache-2.0) AND LicenseRef-musterbau-proprietary")]
    [InlineData("GPL-2.0-or-later WITH Classpath-exception-2.0")]
    [InlineData("mit")]
    [InlineData("DocumentRef-lieferant:LicenseRef-firmware")]
    public async Task Licence_expressions_of_spdx_identifiers_and_licence_refs_are_valid(string expression)
    {
        var bom = Create();
        App(bom)["licenses"] = new JsonArray(new JsonObject { ["expression"] = expression, ["acknowledgement"] = "concluded" });

        Assert.Empty(WithoutLogical(await Validate(bom)));
    }

    [Theory]
    [InlineData("Proprietär")]
    [InlineData("Siemens Software License")]
    [InlineData("MIT OR Hausrecht")]
    [InlineData("LicenseRef-")]
    public async Task Free_text_as_licence_expression_is_invalid(string expression)
    {
        var bom = Create();
        App(bom)["licenses"] = new JsonArray(new JsonObject { ["expression"] = expression, ["acknowledgement"] = "concluded" });

        var report = await Validate(bom);

        Assert.Equal([(Severity.Error, "DISTRIBUTION_LICENCE_INVALID")], WithoutLogical(report));
        var finding = Assert.Single(report.Findings, f => f.Code == "DISTRIBUTION_LICENCE_INVALID");
        Assert.Equal(AppRef, finding.ComponentRef);
        Assert.Equal([TrField.DistributionLicence], finding.Fields);
    }

    [Theory]
    [InlineData("Proprietär", true)]
    [InlineData("LicenseRef-musterbau-proprietary", false)]
    public async Task Licence_name_counts_only_as_licence_ref(string licenceName, bool invalid)
    {
        var bom = Create();
        App(bom)["licenses"] = new JsonArray(new JsonObject
        {
            ["license"] = new JsonObject { ["name"] = licenceName, ["acknowledgement"] = "concluded" },
        });

        var codes = WithoutLogical(await Validate(bom));

        Assert.Equal(invalid, codes.Contains((Severity.Error, "DISTRIBUTION_LICENCE_INVALID")));
        Assert.DoesNotContain((Severity.Error, "DISTRIBUTION_LICENCE_MISSING"), codes);
    }

    [Fact]
    public async Task Sha256_alone_is_not_the_required_hash()
    {
        var bom = Create();
        App(bom)["externalReferences"]![0]!["hashes"] = new JsonArray(new JsonObject { ["alg"] = "SHA-256", ["content"] = new string('b', 64) });

        Assert.Equal([(Severity.Error, "HASH_MISSING")], WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Hash_outside_a_distribution_reference_does_not_count()
    {
        var bom = Create();
        App(bom)["externalReferences"]![0]!["type"] = "website";

        Assert.Equal([(Severity.Error, "HASH_MISSING")], WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Bsi_property_with_wrong_value_is_invalid()
    {
        var bom = Create();
        RemoveProperty(App(bom), "bsi:component:executable");
        AddProperty(App(bom), "bsi:component:executable", "yes");

        Assert.Equal([(Severity.Error, "BSI_PROPERTY_INVALID")], WithoutLogical(await Validate(bom)));
    }

    [Theory]
    [InlineData("bsi:component:filename", "app2.bin")]
    [InlineData("bsi:component:archive", "no archive")]
    [InlineData("bsi:component:effectiveLicence", "MIT")]
    public async Task Bsi_property_twice_is_an_error(string property, string value)
    {
        var bom = Create();
        AddProperty(App(bom), property, value);
        AddProperty(App(bom), property, value);

        Assert.Contains((Severity.Error, "BSI_PROPERTY_DUPLICATE"), Codes(await Validate(bom)));
    }

    [Fact]
    public async Task Effective_licence_with_pdf_spelling_is_a_warning()
    {
        var bom = Create();
        AddProperty(App(bom), "bsi:component:effectiveLicense", "MIT");

        Assert.Equal([(Severity.Warning, "EFFECTIVE_LICENCE_SPELLING")], WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Associated_licences_are_deprecated()
    {
        var bom = Create();
        AddProperty(App(bom), "bsi:component:associatedLicences", "MIT");

        Assert.Equal([(Severity.Warning, "ASSOCIATED_LICENCES_DEPRECATED")], WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Nested_components_are_checked_and_counted()
    {
        var bom = Create();
        var child = App(bom).DeepClone().AsObject();
        child["bom-ref"] = "app-child";
        child.Remove("version");
        child.Remove("components");
        App(bom)["components"] = new JsonArray(child);
        bom["dependencies"]!.AsArray().Add(new JsonObject { ["ref"] = "app-child", ["dependsOn"] = new JsonArray() });
        bom["compositions"]![0]!["dependencies"]!.AsArray().Add("app-child");

        var report = await Validate(bom);

        Assert.Equal(3, report.Components);
        var finding = Assert.Single(report.Findings, f => f.Code == "VERSION_MISSING");
        Assert.Equal("app-child", finding.ComponentRef);
    }

    // ---- References, dependencies, completeness -----------------------------------------------

    [Fact]
    public async Task Component_without_bom_ref_is_an_error()
    {
        var bom = Create();
        App(bom).Remove("bom-ref");

        Assert.Contains((Severity.Error, "BOM_REF_MISSING"), Codes(await Validate(bom)));
    }

    [Fact]
    public async Task Duplicate_bom_ref_is_an_error()
    {
        var bom = Create();
        App(bom)["bom-ref"] = PrimaryRef;

        Assert.Contains((Severity.Error, "BOM_REF_DUPLICATE"), Codes(await Validate(bom)));
    }

    [Fact]
    public async Task Dependency_on_an_unknown_ref_is_an_error()
    {
        var bom = Create();
        bom["dependencies"]![1]!["dependsOn"]!.AsArray().Add("gibt-es-nicht");

        var finding = Assert.Single((await Validate(bom)).Findings, f => f.Code == "DEPENDENCY_REF_UNKNOWN");
        Assert.Contains("gibt-es-nicht", finding.Message);
    }

    [Fact]
    public async Task Component_not_reachable_from_the_primary_component_is_an_error()
    {
        var bom = Create();
        bom["dependencies"]![0]!["dependsOn"] = new JsonArray();

        var report = await Validate(bom);

        var finding = Assert.Single(report.Findings, f => f.Code == "COMPONENT_UNREACHABLE");
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal(AppRef, finding.ComponentRef);
        Assert.Equal([TrField.DependencyGraph], finding.Fields);
    }

    [Fact]
    public async Task Component_reachable_over_another_component_is_fine()
    {
        var bom = Create();
        var lib = App(bom).DeepClone().AsObject();
        lib["bom-ref"] = "lib";
        bom["components"]!.AsArray().Add(lib);
        bom["dependencies"]![1]!["dependsOn"]!.AsArray().Add("lib");
        bom["dependencies"]!.AsArray().Add(new JsonObject { ["ref"] = "lib", ["dependsOn"] = new JsonArray() });
        bom["compositions"]![0]!["dependencies"]!.AsArray().Add("lib");

        Assert.Empty(WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Every_component_needs_a_dependency_entry()
    {
        var bom = Create();
        bom["dependencies"]!.AsArray().RemoveAt(1);

        var finding = Assert.Single((await Validate(bom)).Findings, f => f.Code == "DEPENDENCIES_MISSING");
        Assert.Equal(AppRef, finding.ComponentRef);
    }

    [Fact]
    public async Task Every_component_needs_a_completeness()
    {
        var bom = Create();
        bom["compositions"]![0]!["dependencies"] = new JsonArray(PrimaryRef);

        var finding = Assert.Single((await Validate(bom)).Findings, f => f.Code == "COMPLETENESS_MISSING");
        Assert.Equal(AppRef, finding.ComponentRef);
    }

    [Fact]
    public async Task Composition_without_aggregate_does_not_state_completeness()
    {
        var bom = Create();
        bom["compositions"]![0]!.AsObject().Remove("aggregate");

        var report = await Validate(bom);

        Assert.Equal(2, report.Findings.Count(f => f.Code == "COMPLETENESS_MISSING"));
    }

    // ---- Gaps excuse missing fields ------------------------------------------------------------

    [Fact]
    public async Task Note_gap_excuses_the_missing_hash()
    {
        var bom = Create();
        App(bom).Remove("externalReferences");
        AddProperty(App(bom), "plcsbom:gap", "hash-unavailable");

        var report = await Validate(bom);

        Assert.Equal([(Severity.Note, "HASH_UNAVAILABLE")], WithoutLogical(report));
        Assert.Equal(["hash"], report.Findings.Single(f => f.Code == "HASH_UNAVAILABLE").Fields);
        Assert.Equal(ExitCodes.Ok, report.ExitCode(releaseBuild: true));
    }

    [Fact]
    public async Task Warning_gap_excuses_the_missing_version_but_fails_the_release_build()
    {
        var bom = Create();
        App(bom).Remove("version");
        AddProperty(App(bom), "plcsbom:gap", "version-unresolved");

        var report = await Validate(bom);

        Assert.Equal([(Severity.Warning, "VERSION_UNRESOLVED")], WithoutLogical(report));
        Assert.Equal(ExitCodes.Ok, report.ExitCode(releaseBuild: false));
        Assert.Equal(ExitCodes.Finding, report.ExitCode(releaseBuild: true));
    }

    [Fact]
    public async Task One_gap_can_excuse_several_fields()
    {
        var bom = Create();
        foreach (var p in new[] { "bsi:component:executable", "bsi:component:archive", "bsi:component:structured" })
        {
            RemoveProperty(App(bom), p);
        }
        AddProperty(App(bom), "plcsbom:gap", "file-properties-unknown");

        var report = await Validate(bom);

        Assert.Equal([(Severity.Warning, "FILE_PROPERTIES_UNKNOWN")], WithoutLogical(report));
        Assert.Equal(["executable", "archive", "structured"], report.Findings.Single(f => f.Code == "FILE_PROPERTIES_UNKNOWN").Fields);
    }

    [Fact]
    public async Task Gap_for_another_field_does_not_excuse()
    {
        var bom = Create();
        App(bom).Remove("externalReferences");
        AddProperty(App(bom), "plcsbom:gap", "vendor-sbom-missing");

        var codes = WithoutLogical(await Validate(bom));

        Assert.Contains((Severity.Error, "HASH_MISSING"), codes);
        Assert.Contains((Severity.Note, "VENDOR_SBOM_MISSING"), codes);
    }

    [Fact]
    public async Task Gap_in_the_users_own_namespace_counts()
    {
        var bom = Create();
        App(bom).Remove("externalReferences");
        AddProperty(App(bom), "firma:gap", "hash-unavailable");

        Assert.Equal([(Severity.Note, "HASH_UNAVAILABLE")], WithoutLogical(await Validate(bom)));
    }

    [Theory]
    [InlineData("cdx:gap")]
    [InlineData("bsi:gap")]
    public async Task Gap_in_a_reserved_namespace_does_not_count(string property)
    {
        var bom = Create();
        App(bom).Remove("externalReferences");
        AddProperty(App(bom), property, "hash-unavailable");

        Assert.Equal([(Severity.Error, "HASH_MISSING")], WithoutLogical(await Validate(bom)));
    }

    [Fact]
    public async Task Unknown_gap_code_is_a_warning()
    {
        var bom = Create();
        AddProperty(App(bom), "plcsbom:gap", "firmware-geraten");

        var finding = Assert.Single(WithoutLogical(await Validate(bom)));
        Assert.Equal((Severity.Warning, "GAP_UNKNOWN"), finding);
    }

    [Fact]
    public async Task Same_gap_twice_is_reported_once()
    {
        var bom = Create();
        AddProperty(App(bom), "plcsbom:gap", "vendor-sbom-missing");
        AddProperty(App(bom), "plcsbom:gap", "vendor-sbom-missing");

        Assert.Single((await Validate(bom)).Findings, f => f.Code == "VENDOR_SBOM_MISSING");
    }

    // ---- Logical components (TR 3.2.2) --------------------------------------------------------

    [Fact]
    public async Task Logical_component_needs_no_file_name_hash_or_file_properties()
    {
        var report = await Validate(Create());

        var note = report.Findings.Single(f => f.Code == "LOGICAL_COMPONENT");
        Assert.Equal(PrimaryRef, note.ComponentRef);
        Assert.Equal(["filename", "hash", "executable", "archive", "structured"], note.Fields);
    }

    [Fact]
    public async Task Logical_component_still_needs_a_version()
    {
        var bom = Create();
        bom["metadata"]!["component"]!.AsObject().Remove("version");

        var finding = Assert.Single((await Validate(bom)).Findings, f => f.Code == "VERSION_MISSING");
        Assert.Equal(PrimaryRef, finding.ComponentRef);
    }

    [Fact]
    public async Task Logical_flag_must_be_true()
    {
        var bom = Create();
        var primary = bom["metadata"]!["component"]!.AsObject();
        RemoveProperty(primary, "plcsbom:logicalComponent");
        AddProperty(primary, "plcsbom:logicalComponent", "false");

        var codes = Codes(await Validate(bom));

        Assert.DoesNotContain((Severity.Note, "LOGICAL_COMPONENT"), codes);
        Assert.Contains((Severity.Error, "HASH_MISSING"), codes);
        Assert.Contains((Severity.Error, "FILENAME_MISSING"), codes);
    }

    // ---- Order and counts ---------------------------------------------------------------------

    [Fact]
    public async Task Findings_are_sorted_errors_first_then_by_component_and_code()
    {
        var bom = Create();
        App(bom).Remove("version");
        AddProperty(App(bom), "plcsbom:gap", "vendor-sbom-missing");
        AddProperty(App(bom), "plcsbom:gap", "license-unknown");

        var report = await Validate(bom);

        var severities = report.Findings.Select(f => f.Severity).ToList();
        Assert.Equal(severities.OrderByDescending(s => s), severities);
        Assert.Equal(Severity.Error, report.Findings[0].Severity);
    }

    [Fact]
    public async Task Validation_is_deterministic()
    {
        var bom = Create();
        App(bom).Remove("version");
        App(bom).Remove("licenses");

        var a = await Validate(bom);
        var b = await Validate(bom);

        static IEnumerable<string> Text(ValidationReport r) =>
            r.Findings.Select(f => $"{f.Severity}|{f.Code}|{f.ComponentRef}|{string.Join(',', f.Fields)}|{f.Message}");
        Assert.Equal(Text(a), Text(b));
    }

    [Fact]
    public async Task Stations_are_counted_from_the_own_namespace_only()
    {
        var bom = Create();
        AddProperty(App(bom), "cdx:station", "X");
        AddProperty(App(bom), "firma:station", "PLC_2");

        Assert.Equal(2, (await Validate(bom)).Stations);
    }

    [Fact]
    public async Task Hand_model_golden_file_has_no_errors()
    {
        var report = await SbomValidator.ValidateAsync(await File.ReadAllTextAsync(Golden.PathOf("hand-model.cdx.json"), TestContext.Current.CancellationToken));

        Assert.True(report.SchemaValid);
        Assert.Equal(0, report.Errors);
        Assert.Equal(11, report.Components);
    }
}
