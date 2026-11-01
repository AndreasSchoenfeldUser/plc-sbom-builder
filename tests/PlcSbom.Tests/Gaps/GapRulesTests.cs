using PlcSbom.Core;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Model;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.Gaps;

/// <summary>Gaps that follow from the model alone (GapRules) and the gap catalog.</summary>
public sealed class GapRulesTests
{
    private static readonly Party WithContact = new("Siemens AG", Url: "https://www.siemens.com");
    private static readonly Licensing Licence = new("LicenseRef-siemens-proprietary");

    private static Machine MachineWith(Device? device = null, SoftwareItem? software = null) => new()
    {
        Name = "M",
        Release = "R1",
        Supplier = new Party("Musterbau GmbH", Email: "sbom@musterbau.example"),
        Licensing = new Licensing("MIT"),
        Stations =
        [
            new Station
            {
                Name = "S1",
                AdapterId = AdapterIds.TiaAml,
                SourcePath = "a.aml",
                Devices = device is null ? [] : [device],
                Software = software is null ? [] : [software],
            },
        ],
    };

    private static Device Cpu => new()
    {
        TypeName = "CPU 1511-1 PN",
        Vendor = WithContact,
        Firmware = "V2.8",
        FirmwareOrigin = FirmwareOrigin.Projected,
        FirmwareLicensing = Licence,
    };

    private static SoftwareItem Library => new()
    {
        Kind = SoftwareKind.Library,
        Name = "LGF",
        Vendor = WithContact,
        Version = "7.0.1",
        Traits = new FileTraits(false, false, true),
        FileName = "LGF.zal21",
        DeployableUri = "https://example.invalid/LGF.zal21",
        DeployableSha512 = new string('a', 128),
        Licensing = Licence,
    };

    private static List<string> DeviceGaps(Device device) =>
        GapRules.Apply(MachineWith(device: device)).Stations[0].Devices[0].Gaps.Select(g => g.Code).ToList();

    private static List<string> SoftwareGaps(SoftwareItem item) =>
        GapRules.Apply(MachineWith(software: item)).Stations[0].Software[0].Gaps.Select(g => g.Code).ToList();

    [Fact]
    public void Projected_firmware_gets_the_gaps_vendor_firmware_always_has()
    {
        Assert.Equal(
            [GapCatalog.FirmwareProjectedOnly, GapCatalog.HashUnavailable, GapCatalog.FilenameUnavailable, GapCatalog.VendorSbomMissing],
            DeviceGaps(Cpu));
    }

    [Fact]
    public void As_found_firmware_is_not_projected_only()
    {
        Assert.DoesNotContain(GapCatalog.FirmwareProjectedOnly, DeviceGaps(Cpu with { FirmwareOrigin = FirmwareOrigin.AsFound }));
    }

    [Fact]
    public void Device_without_firmware_gets_firmware_not_in_project()
    {
        var gaps = DeviceGaps(Cpu with { Firmware = null, FirmwareOrigin = FirmwareOrigin.NotInProject });

        Assert.Contains(GapCatalog.FirmwareNotInProject, gaps);
        Assert.DoesNotContain(GapCatalog.FirmwareProjectedOnly, gaps);
    }

    [Fact]
    public void Passive_device_gets_no_gaps()
    {
        Assert.Empty(DeviceGaps(new Device { TypeName = "Rack", IsPassive = true }));
    }

    [Fact]
    public void Unknown_vendor_is_supplier_unknown()
    {
        Assert.Contains(GapCatalog.SupplierUnknown, DeviceGaps(Cpu with { Vendor = null }));
    }

    [Fact]
    public void Vendor_without_contact_is_creator_contact_unknown()
    {
        var gaps = DeviceGaps(Cpu with { Vendor = new Party("Siemens AG") });

        Assert.Contains(GapCatalog.CreatorContactUnknown, gaps);
        Assert.DoesNotContain(GapCatalog.SupplierUnknown, gaps);
    }

    [Fact]
    public void Firmware_without_licence_is_licence_unknown()
    {
        Assert.Contains(GapCatalog.LicenseUnknown, DeviceGaps(Cpu with { FirmwareLicensing = null }));
    }

    [Fact]
    public void Fully_described_library_gets_no_gaps()
    {
        Assert.Empty(SoftwareGaps(Library));
    }

    [Fact]
    public void Placeholder_version_is_version_unresolved()
    {
        Assert.Contains(GapCatalog.VersionUnresolved, SoftwareGaps(Library with { Version = "*", VersionResolved = false }));
        Assert.Contains(GapCatalog.VersionUnresolved, SoftwareGaps(Library with { Version = null }));
    }

    [Fact]
    public void Runtime_without_version_is_runtime_version_unknown()
    {
        var gaps = SoftwareGaps(Library with { Kind = SoftwareKind.Runtime, Version = null });

        Assert.Contains(GapCatalog.RuntimeVersionUnknown, gaps);
        Assert.DoesNotContain(GapCatalog.VersionUnresolved, gaps);
    }

    [Fact]
    public void Unknown_file_traits_are_file_properties_unknown()
    {
        Assert.Contains(GapCatalog.FilePropertiesUnknown, SoftwareGaps(Library with { Traits = null }));
    }

    [Fact]
    public void Missing_file_name_is_filename_unavailable()
    {
        Assert.Contains(GapCatalog.FilenameUnavailable, SoftwareGaps(Library with { FileName = null }));
    }

    [Fact]
    public void Library_without_hash_is_hash_unavailable()
    {
        var gaps = SoftwareGaps(Library with { DeployableSha512 = null, DeployableUri = null });

        Assert.Contains(GapCatalog.HashUnavailable, gaps);
        Assert.DoesNotContain(GapCatalog.ProgramSourceMissing, gaps);
    }

    [Fact]
    public void Program_without_source_directory_is_program_source_missing()
    {
        var program = Library with { Kind = SoftwareKind.UserProgram, DeployableSha512 = null, DeployableUri = null };

        var gaps = SoftwareGaps(program);

        Assert.Contains(GapCatalog.ProgramSourceMissing, gaps);
        Assert.DoesNotContain(GapCatalog.HashUnavailable, gaps);
    }

    [Fact]
    public void Program_with_source_hash_is_only_hash_unavailable()
    {
        var program = Library with
        {
            Kind = SoftwareKind.UserProgram,
            DeployableSha512 = null,
            DeployableUri = null,
            SourceUri = "https://git.example/tree/x",
            SourceSha512 = new string('b', 128),
        };

        var gaps = SoftwareGaps(program);

        Assert.Contains(GapCatalog.HashUnavailable, gaps);
        Assert.DoesNotContain(GapCatalog.ProgramSourceMissing, gaps);
    }

    [Fact]
    public void Machine_without_licence_is_licence_unknown()
    {
        var machine = GapRules.Apply(MachineWith() with { Licensing = null });

        Assert.Contains(machine.Gaps, g => g.Code == GapCatalog.LicenseUnknown);
    }

    [Fact]
    public void Unassigned_device_is_device_unassigned_unless_passive()
    {
        var machine = GapRules.Apply(MachineWith() with
        {
            UnassignedDevices = [Cpu, new Device { TypeName = "BaseUnit", IsPassive = true }],
        });

        Assert.Contains(machine.UnassignedDevices[0].Gaps, g => g.Code == GapCatalog.DeviceUnassigned);
        Assert.Empty(machine.UnassignedDevices[1].Gaps);
    }

    [Fact]
    public void Existing_gaps_are_kept_and_not_duplicated()
    {
        var device = Cpu with { Gaps = [new Gap(GapCatalog.HashUnavailable, "vom Adapter"), new Gap(GapCatalog.EkeyDisabled, "vom Adapter")] };

        var gaps = GapRules.Apply(MachineWith(device: device)).Stations[0].Devices[0].Gaps;

        Assert.Single(gaps, g => g.Code == GapCatalog.HashUnavailable);
        Assert.Equal("vom Adapter", gaps.Single(g => g.Code == GapCatalog.HashUnavailable).Message);
        Assert.Contains(gaps, g => g.Code == GapCatalog.EkeyDisabled);
    }

    [Fact]
    public void Applying_twice_changes_nothing()
    {
        var once = GapRules.Apply(HandModel.Create());
        var twice = GapRules.Apply(once);

        static IEnumerable<string> All(Machine m) =>
            m.Gaps.Select(g => g.Code)
                .Concat(m.UnassignedDevices.SelectMany(d => d.Gaps.Select(g => "u:" + g.Code)))
                .Concat(m.Stations.SelectMany(s =>
                    s.Devices.SelectMany(d => d.Gaps.Select(g => s.Name + "/" + d.TypeName + ":" + g.Code))
                        .Concat(s.Software.SelectMany(x => x.Gaps.Select(g => s.Name + "/" + x.Name + ":" + g.Code)))));

        Assert.Equal(All(once), All(twice));
    }

    // ---- Catalog ------------------------------------------------------------------------------

    [Fact]
    public void Catalog_codes_are_unique_kebab_case()
    {
        var codes = GapCatalog.All.Select(g => g.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", c));
    }

    [Fact]
    public void No_catalog_gap_is_an_error()
    {
        // A gap is recorded on purpose; errors are only for missing fields without a gap (CLAUDE.md 4.4).
        Assert.DoesNotContain(GapCatalog.All, g => g.Severity == Severity.Error);
    }

    [Fact]
    public void Every_catalog_gap_has_a_german_description()
    {
        Assert.All(GapCatalog.All, g => Assert.EndsWith(".", g.Description));
    }

    [Theory]
    [InlineData(GapCatalog.HashUnavailable, Severity.Note)]
    [InlineData(GapCatalog.FirmwareNotInProject, Severity.Note)]
    [InlineData(GapCatalog.VersionUnresolved, Severity.Warning)]
    [InlineData(GapCatalog.LibrariesNotRead, Severity.Warning)]
    [InlineData(GapCatalog.DeviceUnassigned, Severity.Note)]
    public void Severities_follow_section_4_4(string code, Severity severity)
    {
        Assert.Equal(severity, GapCatalog.Find(code)!.Severity);
    }

    [Fact]
    public void Unknown_code_is_not_found()
    {
        Assert.Null(GapCatalog.Find("gibt-es-nicht"));
    }

    [Fact]
    public void Every_gap_code_says_what_closes_it()
    {
        Assert.All(GapCatalog.All, g => Assert.False(string.IsNullOrWhiteSpace(GapCatalog.Remedy(g.Code)), g.Code));
        Assert.Null(GapCatalog.Remedy("unbekannt"));
    }
}
