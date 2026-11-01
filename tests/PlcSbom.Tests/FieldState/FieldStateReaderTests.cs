using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.FieldState;
using PlcSbom.Tests.Samples;

namespace PlcSbom.Tests.FieldState;

public sealed class FieldStateReaderTests
{
    /// <summary>Field file with one device whose webapi holds the given fixture responses (fixtures/feld/s7-webapi).</summary>
    internal static byte[] WebApiFile(string actual = "im0-actual-cpu1518.json", string? configured = "im0-configured-cpu1518.json", string extra = "")
    {
        static string R(string name) => Encoding.UTF8.GetString(Fixtures.Read($"feld/s7-webapi/{name}"));
        var configuredPart = configured is null ? "" : $", \"configured\": {R(configured)}";
        return Encoding.UTF8.GetBytes($$"""
            { "machine": "Maschine XY", "serialNumber": "7", "date": "2026-10-22", "source": "abnahme",
              "devices": [ { "station": "PLC_1"{{extra}}, "webapi": { "actual": {{R(actual)}}{{configuredPart}} } } ] }
            """);
    }

    private static UsageException Invalid(string json) =>
        Assert.Throws<UsageException>(() => FieldStateReader.Parse(Encoding.UTF8.GetBytes(json), "feld/x.json"));

    [Fact]
    public void Sample_field_file_is_read()
    {
        var field = SampleMachine.Field();

        Assert.Equal("Maschine XY", field.Machine);
        Assert.Equal("1042", field.SerialNumber);
        Assert.Equal(new DateOnly(2026, 10, 22), field.Date);
        Assert.Equal("inbetriebnahme", field.Source);
        Assert.Equal(5, field.Devices.Count);
        var cpu = field.Devices[0];
        Assert.Equal(("PLC_1", "S7-1200 station_1/0/1", "6ES7 214-1AG40-0XB0", "V4.6.1", "5", "S V-L9A12345", "webserver"),
            (cpu.Station, cpu.Position, cpu.OrderNumber, cpu.Firmware, cpu.HardwareRevision, cpu.Serial, cpu.Via));
        Assert.Null(field.Devices[4].OrderNumber);
        Assert.Equal(64, field.Sha256.Length);
    }

    [Fact]
    public void Web_api_responses_give_order_number_firmware_and_serial()
    {
        var field = FieldStateReader.Parse(WebApiFile(), "feld/webapi.json");

        var device = Assert.Single(field.Devices);
        Assert.Equal("6ES7 518-3AT10-0AB0", device.OrderNumber); // the CPU pads with a space
        Assert.Equal("V4.1.0", device.Firmware);
        Assert.Equal("0", device.HardwareRevision);
        Assert.Equal("10S C-521E1fFj24", device.Serial);
        Assert.Equal("webapi", device.Via);
        Assert.Equal("6ES7 518-3AT10-0AB0", device.ConfiguredOrderNumber);
        Assert.Equal("V4.1.0", device.ConfiguredFirmware);
        Assert.Empty(field.Warnings);
    }

    [Fact]
    public void Web_api_configured_response_is_optional()
    {
        var device = Assert.Single(FieldStateReader.Parse(WebApiFile(configured: null), "f.json").Devices);

        Assert.Null(device.ConfiguredFirmware);
        Assert.Equal("V4.1.0", device.Firmware);
    }

    [Fact]
    public void Web_api_response_other_than_im0_is_invalid()
    {
        var ex = Assert.Throws<UsageException>(() => FieldStateReader.Parse(WebApiFile(actual: "im1-actual-cpu1518.json"), "f.json"));

        Assert.Equal("FIELD_INVALID", ex.Code);
        Assert.Contains("IM0", ex.Message);
    }

    [Fact]
    public void Web_api_error_response_skips_the_device_with_a_warning()
    {
        var json = """
            { "machine": "M", "serialNumber": "1", "date": "2026-10-22",
              "devices": [ { "webapi": { "actual": { "jsonrpc": "2.0", "id": "1", "error": { "code": 2, "message": "Permission denied" } } } } ] }
            """;

        var field = FieldStateReader.Parse(Encoding.UTF8.GetBytes(json), "f.json");

        Assert.Empty(field.Devices);
        var warning = Assert.Single(field.Warnings);
        Assert.Equal("FIELD_WEBAPI_ERROR", warning.Code);
        Assert.Contains("Permission denied", warning.Message);
    }

    [Fact]
    public void Web_api_together_with_values_is_invalid()
    {
        var ex = Assert.Throws<UsageException>(() => FieldStateReader.Parse(WebApiFile(extra: ", \"firmware\": \"V1.0\""), "f.json"));

        Assert.Contains("„webapi“ und „firmware“", ex.Message);
    }

    [Theory]
    [InlineData("""{ "machine": "M", "serialNumber": "1", "date": "2026-10-22", "devices": [], "extra": 1 }""", "Unbekannter Schlüssel „extra“")]
    [InlineData("""{ "machine": "M", "serialNumber": "1", "date": "2026-10-22", "devices": [ { "orderNumber": "A", "firmwre": "V1" } ] }""", "Unbekannter Schlüssel „firmwre“")]
    [InlineData("""{ "serialNumber": "1", "date": "2026-10-22", "devices": [] }""", "„machine“ fehlt")]
    [InlineData("""{ "machine": "M", "serialNumber": "1", "date": "22.10.2026", "devices": [] }""", "„date“ muss ein Datum")]
    [InlineData("""{ "machine": "M", "serialNumber": "1", "date": "2026-10-22" }""", "„devices“ fehlt")]
    [InlineData("""{ "machine": "M", "serialNumber": "1", "date": "2026-10-22", "devices": [ { "firmware": "V1" } ] }""", "braucht „position“ oder „orderNumber“")]
    [InlineData("""{ "machine": "M", "serialNumber": "1", "date": "2026-10-22", "devices": [ { "orderNumber": 5 } ] }""", "muss Text sein")]
    [InlineData("""{ "machine": "M", "machine": "N", "serialNumber": "1", "date": "2026-10-22", "devices": [] }""", "Kein gültiges JSON")]
    [InlineData("""[]""", "JSON-Objekt")]
    public void Invalid_field_files_are_exit_2(string json, string message)
    {
        var ex = Invalid(json);

        Assert.Equal("FIELD_INVALID", ex.Code);
        Assert.Equal(ExitCodes.Usage, ex.ExitCode);
        Assert.Contains(message, ex.Message);
        Assert.StartsWith("feld/x.json: ", ex.Message);
    }

    [Fact]
    public void Hardware_revision_may_be_a_number()
    {
        var field = FieldStateReader.Parse(Encoding.UTF8.GetBytes("""
            { "machine": "M", "serialNumber": "1", "date": "2026-10-22", "devices": [ { "orderNumber": "A", "hardwareRevision": 3 } ] }
            """), "f.json");

        Assert.Equal("3", field.Devices[0].HardwareRevision);
    }

    [Fact]
    public void Missing_file_is_exit_2()
    {
        var ex = Assert.Throws<UsageException>(() => FieldStateReader.Load(Path.Combine(Path.GetTempPath(), $"fehlt-{Guid.NewGuid():N}.json")));

        Assert.Equal("FIELD_NOT_FOUND", ex.Code);
    }
}
