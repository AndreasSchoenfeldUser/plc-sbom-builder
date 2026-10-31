using System.Security.Cryptography;
using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Model;

namespace PlcSbom.Tests.Samples;

/// <summary>
/// Hand-built model for M1 (CLAUDE.md section 12): no adapter involved. Device data follows the
/// fixtures named in CLAUDE.md section 13, everything else is invented test data and stands for
/// what the adapters and machine.yml will deliver later.
/// </summary>
public static class HandModel
{
    public const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";

    public static readonly DateTimeOffset Timestamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private static readonly Party Musterbau = new("Musterbau GmbH", Email: "sbom@musterbau.example");
    private static readonly Party Siemens = new("Siemens AG", Url: "https://www.siemens.com");
    private static readonly Party Beckhoff = new("Beckhoff Automation GmbH & Co. KG", Url: "https://www.beckhoff.com");

    private static readonly Licensing MusterbauLicence = new("LicenseRef-musterbau-proprietary", Effective: "LicenseRef-musterbau-proprietary");
    private static readonly Licensing SiemensLicence = new("LicenseRef-siemens-proprietary");

    public static Machine Create() => new()
    {
        Name = "Maschine XY",
        Release = "R2026.10",
        Commit = Commit,
        Supplier = Musterbau,
        Licensing = MusterbauLicence,
        Stations = [Plc1(), Roboterzelle()],
        UnassignedDevices =
        [
            new Device
            {
                TypeName = "TP700 Comfort",
                Vendor = Siemens,
                VendorOrigin = ValueOrigin.Assumed,
                OrderNumber = "6AV2 124-0GC01-0AX0",
                DeviceName = "HMI_1",
                Firmware = "V17.0.0.0",
                FirmwareOrigin = FirmwareOrigin.Projected,
                FirmwareLicensing = SiemensLicence,
            },
        ],
    };

    private static Station Plc1() => new()
    {
        Name = "PLC_1",
        AdapterId = AdapterIds.TiaAml,
        SourcePath = "export/Linie1.aml",
        SourceSha256 = Sha256("export/Linie1.aml"),
        SourceExportedAt = new DateTimeOffset(2026, 10, 22, 8, 15, 0, TimeSpan.Zero),
        EngineeringTool = new ToolInfo("TIA Portal", "V21", "2100.0.121.1", "Siemens AG"),
        Devices =
        [
            new Device
            {
                TypeName = "Rack",
                OrderNumber = "6ES7 590-1***0-0AA0",
                OrderNumberIsPattern = true,
                Position = "0",
                IsPassive = true,
            },
            new Device
            {
                TypeName = "CPU 1511-1 PN",
                Vendor = Siemens,
                VendorOrigin = ValueOrigin.Assumed,
                OrderNumber = "6ES7 511-1AK02-0AB0",
                DeviceName = "PLC_1",
                Position = "0/1",
                Classification = "CPU",
                IsController = true,
                Firmware = "V2.8",
                FirmwareOrigin = FirmwareOrigin.Projected,
                FirmwareLicensing = SiemensLicence,
            },
            new Device
            {
                TypeName = "PM 70W 120/230VAC",
                Vendor = Siemens,
                VendorOrigin = ValueOrigin.Assumed,
                OrderNumber = "6EP1332-4BA00",
                Position = "0/0",
                FirmwareOrigin = FirmwareOrigin.NotInProject,
                FirmwareLicensing = SiemensLicence,
            },
            new Device
            {
                TypeName = "IM 155-6 PN HF",
                Vendor = Siemens,
                VendorOrigin = ValueOrigin.Assumed,
                OrderNumber = "6ES7 155-6AU01-0BN0",
                DeviceName = "IO_Device_Main",
                Position = "1/0",
                Classification = "HeadModule",
                Firmware = "V4.2",
                FirmwareOrigin = FirmwareOrigin.Projected,
                FirmwareLicensing = SiemensLicence,
            },
        ],
        Software =
        [
            new SoftwareItem
            {
                Kind = SoftwareKind.UserProgram,
                Name = "PLC_1, Anwenderprogramm",
                Vendor = Musterbau,
                Version = "R2026.10 (git a1b2c3d)",
                Traits = new FileTraits(Executable: true, Archive: false, Structured: false),
                Licensing = MusterbauLicence,
                SourceUri = "https://git.musterbau.example/maschine-xy/-/tree/" + Commit + "/tia/PLC_1",
                SourceSha512 = Sha512("tia/PLC_1"),
            },
            new SoftwareItem
            {
                Kind = SoftwareKind.Library,
                Name = "LGF",
                Vendor = Siemens,
                Version = "7.0.1",
                Origin = ValueOrigin.Declared,
                VendorOrigin = ValueOrigin.Declared,
                Licensing = SiemensLicence,
            },
        ],
        Gaps = [new Gap("program-not-read", "Der AML-Export enthält keine Bausteine.")],
    };

    private static Station Roboterzelle() => new()
    {
        Name = "Roboterzelle",
        AdapterId = AdapterIds.TwinCat,
        SourcePath = "twincat/Zelle/Zelle.tsproj",
        SourceSha256 = Sha256("twincat/Zelle/Zelle.tsproj"),
        EngineeringTool = new ToolInfo("TwinCAT XAE", "3.1.4024.62", Vendor: "Beckhoff Automation GmbH & Co. KG"),
        Devices =
        [
            new Device
            {
                TypeName = "EK1100",
                Vendor = Beckhoff,
                OrderNumber = "EK1100",
                DeviceName = "Term 1 (EK1100)",
                FirmwareOrigin = FirmwareOrigin.NotInProject,
                VendorKeys = new Dictionary<string, string>
                {
                    ["VendorId"] = "0x00000002",
                    ["ProductCode"] = "0x044c2c52",
                    ["RevisionNo"] = "0x00120000",
                },
            },
        ],
        Software =
        [
            new SoftwareItem
            {
                Kind = SoftwareKind.UserProgram,
                Name = "Roboterzelle, Anwenderprogramm",
                Vendor = Musterbau,
                Version = "R2026.10 (git a1b2c3d)",
                Traits = new FileTraits(Executable: true, Archive: false, Structured: false),
                Licensing = MusterbauLicence,
            },
            new SoftwareItem
            {
                Kind = SoftwareKind.Runtime,
                Name = "TwinCAT 3 Runtime",
                Vendor = Beckhoff,
            },
            new SoftwareItem
            {
                Kind = SoftwareKind.Library,
                Name = "Tc2_Standard",
                Vendor = Beckhoff,
                Version = "*",
                VersionResolved = false,
            },
        ],
    };

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Sha512(string text) => Convert.ToHexStringLower(SHA512.HashData(Encoding.UTF8.GetBytes(text)));
}
