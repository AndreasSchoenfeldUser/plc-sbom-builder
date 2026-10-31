using PlcSbom.Core.Model;

namespace PlcSbom.Core.Gaps;

/// <summary>
/// Adds the gaps that follow from the model alone, independent of the adapter: a value is null, so the
/// matching gap is recorded. Adapters add source-specific gaps themselves (e.g. ekey-disabled).
/// Existing gaps are kept; no code is added twice to the same element.
/// </summary>
public static class GapRules
{
    public static Machine Apply(Machine machine) => machine with
    {
        Gaps = Add(machine.Gaps, machine.Licensing?.Distribution is null ? GapCatalog.LicenseUnknown : null),
        Stations = machine.Stations.Select(Apply).ToList(),
        UnassignedDevices = machine.UnassignedDevices
            .Select(Apply)
            .Select(d => d.IsPassive ? d : d with { Gaps = Add(d.Gaps, GapCatalog.DeviceUnassigned) })
            .ToList(),
    };

    private static Station Apply(Station station) => station with
    {
        Devices = station.Devices.Select(Apply).ToList(),
        Software = station.Software.Select(Apply).ToList(),
    };

    private static Device Apply(Device device)
    {
        if (device.IsPassive)
        {
            return device;
        }
        return device with
        {
            Gaps = Add(
                device.Gaps,
                VendorGap(device.Vendor),
                device.Firmware is null ? GapCatalog.FirmwareNotInProject : null,
                device.Firmware is not null && device.FirmwareOrigin == FirmwareOrigin.Projected ? GapCatalog.FirmwareProjectedOnly : null,
                // Vendor firmware never reaches the tool as a file: no SHA-512, no file name (TR 3.2.1).
                GapCatalog.HashUnavailable,
                GapCatalog.FilenameUnavailable,
                device.FirmwareLicensing?.Distribution is null ? GapCatalog.LicenseUnknown : null,
                GapCatalog.VendorSbomMissing),
        };
    }

    private static SoftwareItem Apply(SoftwareItem item) => item with
    {
        Gaps = Add(
            item.Gaps,
            VendorGap(item.Vendor),
            item.Version is null || !item.VersionResolved
                ? item.Kind == SoftwareKind.Runtime ? GapCatalog.RuntimeVersionUnknown : GapCatalog.VersionUnresolved
                : null,
            item.Traits is null ? GapCatalog.FilePropertiesUnknown : null,
            item.DeployableSha512 is null
                ? item.Kind == SoftwareKind.UserProgram && item.SourceSha512 is null ? GapCatalog.ProgramSourceMissing : GapCatalog.HashUnavailable
                : null,
            item.FileName is null ? GapCatalog.FilenameUnavailable : null,
            item.Licensing?.Distribution is null ? GapCatalog.LicenseUnknown : null),
    };

    private static string? VendorGap(Party? vendor) =>
        vendor is null ? GapCatalog.SupplierUnknown
        : !vendor.HasContact ? GapCatalog.CreatorContactUnknown
        : null;

    private static IReadOnlyList<Gap> Add(IReadOnlyList<Gap> existing, params string?[] codes)
    {
        var result = existing.ToList();
        foreach (var code in codes)
        {
            if (code is not null && result.All(g => g.Code != code))
            {
                result.Add(new Gap(code, GapCatalog.Find(code)!.Description));
            }
        }
        return result;
    }
}
