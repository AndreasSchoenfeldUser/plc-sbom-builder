using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlcSbom.Core.FieldState;

/// <summary>As-found state of one machine, read from a field file (CLAUDE.md section 8.3).</summary>
public sealed record AsFoundState
{
    /// <summary>Path as given on the command line, with '/'.</summary>
    public required string SourcePath { get; init; }

    public required string Sha256 { get; init; }

    /// <summary>Name of the machine; has to equal the name in the SBOM.</summary>
    public required string Machine { get; init; }

    /// <summary>Serial number of the machine the state was read from.</summary>
    public required string SerialNumber { get; init; }

    /// <summary>Day the state was read.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Occasion, e.g. inbetriebnahme, abnahme, service.</summary>
    public string? Source { get; init; }

    public required IReadOnlyList<FieldDevice> Devices { get; init; }

    /// <summary>Warnings while reading, e.g. an error response of the Web API.</summary>
    public IReadOnlyList<FieldWarning> Warnings { get; init; } = [];
}

/// <summary>One device as found in the field.</summary>
public sealed record FieldDevice
{
    /// <summary>1-based position in the file, for messages.</summary>
    public required int Number { get; init; }

    public string? Station { get; init; }

    /// <summary>Position as in the SBOM (&lt;ns&gt;:device:position), e.g. "S7-1200 station_1/0/1".</summary>
    public string? Position { get; init; }

    public string? OrderNumber { get; init; }

    /// <summary>Firmware as read, e.g. V4.6.1.</summary>
    public string? Firmware { get; init; }

    public string? HardwareRevision { get; init; }

    /// <summary>Serial number of the device.</summary>
    public string? Serial { get; init; }

    /// <summary>How the state was read, e.g. webapi, proneta, manuell.</summary>
    public string? Via { get; init; }

    /// <summary>Web API only: the configuration loaded into the CPU (IM0 "configured").</summary>
    public string? ConfiguredOrderNumber { get; init; }

    public string? ConfiguredFirmware { get; init; }

    /// <summary>Short text for messages: position, otherwise order number, otherwise the number.</summary>
    public string Label => Position ?? OrderNumber ?? $"Eintrag {Number}";
}

public sealed record FieldWarning(string Code, string Message);

/// <summary>
/// Reads a field file. The format is plc-sbom's own (CLAUDE.md section 8.3): machine, serial number,
/// date and a list of devices. Instead of the values, a device may carry the responses of the S7-1500
/// Web API to Modules.ReadIdentificationMaintenance with IM0, "actual" and optionally "configured",
/// unchanged as the CPU sent them (fixtures/feld/s7-webapi). Unknown keys are errors, like in machine.yml.
/// </summary>
public static class FieldStateReader
{
    private static readonly string[] TopKeys = ["machine", "serialNumber", "date", "source", "devices"];
    private static readonly string[] DeviceKeys = ["station", "position", "orderNumber", "firmware", "hardwareRevision", "serial", "via", "webapi"];
    private static readonly string[] ValueKeys = ["orderNumber", "firmware", "hardwareRevision", "serial"];
    private static readonly string[] WebApiKeys = ["actual", "configured"];

    public static AsFoundState Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new UsageException("FIELD_NOT_FOUND", $"Die Feld-Datei {path} gibt es nicht.");
        }
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UsageException("FIELD_NOT_READABLE", $"Die Feld-Datei {path} ist nicht lesbar: {ex.Message}", ex);
        }
        return Parse(bytes, path.Replace('\\', '/'));
    }

    public static AsFoundState Parse(byte[] bytes, string displayPath)
    {
        UsageException Error(string message) => new("FIELD_INVALID", $"{displayPath}: {message}");

        JsonObject root;
        try
        {
            root = JsonNode.Parse(bytes, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false }) as JsonObject
                ?? throw Error("Die Datei muss ein JSON-Objekt mit machine, serialNumber, date und devices sein.");
        }
        catch (JsonException ex)
        {
            throw new UsageException("FIELD_INVALID", $"{displayPath}: Kein gültiges JSON: {ex.Message}", ex);
        }

        CheckKeys(root, TopKeys, "", Error);
        var machine = Required(root, "machine", "", Error);
        var serialNumber = Required(root, "serialNumber", "", Error);
        if (!DateOnly.TryParseExact(Required(root, "date", "", Error), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw Error("„date“ muss ein Datum wie 2026-10-22 sein.");
        }
        if (root["devices"] is not JsonArray list)
        {
            throw Error("„devices“ fehlt oder ist keine Liste.");
        }

        var devices = new List<FieldDevice>();
        var warnings = new List<FieldWarning>();
        for (var i = 0; i < list.Count; i++)
        {
            var at = $"devices[{i}]";
            if (list[i] is not JsonObject device)
            {
                throw Error($"{at} ist kein Objekt.");
            }
            CheckKeys(device, DeviceKeys, at, Error);
            var entry = new FieldDevice
            {
                Number = i + 1,
                Station = Optional(device, "station", at, Error),
                Position = Optional(device, "position", at, Error),
                Via = Optional(device, "via", at, Error),
            };
            if (device["webapi"] is { } webapi)
            {
                if (ValueKeys.FirstOrDefault(device.ContainsKey) is { } both)
                {
                    throw Error($"{at} nennt „webapi“ und „{both}“. Mit „webapi“ kommen Bestellnummer, Firmware, Hardware-Stand und Seriennummer aus den Antworten der CPU.");
                }
                var read = WebApi(webapi, $"{at}.webapi", Error, warnings);
                if (read is null)
                {
                    continue;
                }
                entry = entry with
                {
                    OrderNumber = read.Actual.OrderNumber,
                    Firmware = read.Actual.Firmware,
                    HardwareRevision = read.Actual.HardwareRevision,
                    Serial = read.Actual.Serial,
                    Via = entry.Via ?? "webapi",
                    ConfiguredOrderNumber = read.Configured?.OrderNumber,
                    ConfiguredFirmware = read.Configured?.Firmware,
                };
            }
            else
            {
                entry = entry with
                {
                    OrderNumber = Optional(device, "orderNumber", at, Error),
                    Firmware = Optional(device, "firmware", at, Error),
                    HardwareRevision = Optional(device, "hardwareRevision", at, Error, allowNumber: true),
                    Serial = Optional(device, "serial", at, Error),
                };
            }
            if (entry.Position is null && entry.OrderNumber is null)
            {
                throw Error($"{at} braucht „position“ oder „orderNumber“, sonst lässt es sich keinem Gerät der SBOM zuordnen.");
            }
            devices.Add(entry);
        }

        return new AsFoundState
        {
            SourcePath = displayPath,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Machine = machine,
            SerialNumber = serialNumber,
            Date = date,
            Source = Optional(root, "source", "", Error),
            Devices = devices,
            Warnings = warnings,
        };
    }

    private sealed record Im0(string OrderNumber, string? Firmware, string? HardwareRevision, string? Serial);

    private sealed record WebApiRead(Im0 Actual, Im0? Configured);

    /// <summary>
    /// JSON-RPC responses of Modules.ReadIdentificationMaintenance, IM0 (fixtures/feld/s7-webapi): result.data
    /// with order_number, serial_number, hardware_revision and software_revision {type, major, minor, patch}.
    /// The CPU pads text with spaces; they are trimmed. An error response skips the device with a warning.
    /// </summary>
    private static WebApiRead? WebApi(JsonNode node, string at, Func<string, UsageException> error, List<FieldWarning> warnings)
    {
        if (node is not JsonObject webapi)
        {
            throw error($"{at} ist kein Objekt mit „actual“ und „configured“.");
        }
        CheckKeys(webapi, WebApiKeys, at, error);
        if (webapi["actual"] is null)
        {
            throw error($"{at}.actual fehlt: die Antwort der CPU auf Modules.ReadIdentificationMaintenance mit number 0 und type actual.");
        }
        var actual = Response(webapi["actual"]!, $"{at}.actual", error, warnings);
        if (actual is null)
        {
            return null;
        }
        var configured = webapi["configured"] is { } c ? Response(c, $"{at}.configured", error, warnings) : null;
        return new WebApiRead(actual, configured);
    }

    private static Im0? Response(JsonNode node, string at, Func<string, UsageException> error, List<FieldWarning> warnings)
    {
        if (node is not JsonObject response)
        {
            throw error($"{at} ist keine JSON-RPC-Antwort.");
        }
        if (response["error"] is JsonObject rpcError)
        {
            warnings.Add(new FieldWarning("FIELD_WEBAPI_ERROR",
                $"{at}: Die CPU hat mit Fehler {rpcError["code"]?.ToJsonString() ?? "?"} geantwortet ({Trimmed(rpcError["message"]) ?? "ohne Text"}). Das Gerät ist nicht abgeglichen."));
            return null;
        }
        if (response["result"]?["data"] is not JsonObject data || Trimmed(data["order_number"]) is not { } orderNumber)
        {
            throw error($"{at} ist keine Antwort auf Modules.ReadIdentificationMaintenance mit IM0 (result.data.order_number fehlt).");
        }
        string? firmware = null;
        if (data["software_revision"] is JsonObject revision)
        {
            if (Number(revision["major"]) is not { } major || Number(revision["minor"]) is not { } minor)
            {
                throw error($"{at}: software_revision braucht major und minor.");
            }
            var patch = Number(revision["patch"]);
            firmware = $"{Trimmed(revision["type"])}{major}.{minor}{(patch is null ? "" : $".{patch}")}";
        }
        return new Im0(orderNumber, firmware, Number(data["hardware_revision"])?.ToString(CultureInfo.InvariantCulture), Trimmed(data["serial_number"]));
    }

    private static void CheckKeys(JsonObject node, string[] allowed, string at, Func<string, UsageException> error)
    {
        foreach (var (key, _) in node)
        {
            if (!allowed.Contains(key, StringComparer.Ordinal))
            {
                var where = at.Length == 0 ? "" : $"{at}: ";
                throw error($"{where}Unbekannter Schlüssel „{key}“. Erlaubt sind: {string.Join(", ", allowed)}.");
            }
        }
    }

    private static string Required(JsonObject node, string key, string at, Func<string, UsageException> error) =>
        Optional(node, key, at, error) ?? throw error($"{(at.Length == 0 ? "" : $"{at}: ")}„{key}“ fehlt.");

    private static string? Optional(JsonObject node, string key, string at, Func<string, UsageException> error, bool allowNumber = false)
    {
        var where = at.Length == 0 ? "" : $"{at}: ";
        return node[key] switch
        {
            null => node.ContainsKey(key) ? throw error($"{where}„{key}“ ist null.") : null,
            JsonValue v when v.TryGetValue<string>(out var text) => text.Trim().Length == 0 ? throw error($"{where}„{key}“ ist leer.") : text.Trim(),
            JsonValue v when allowNumber && Number(v) is { } n => n.ToString(CultureInfo.InvariantCulture),
            _ => throw error($"{where}„{key}“ muss Text sein."),
        };
    }

    private static long? Number(JsonNode? node) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<long>(out var n) ? n : null;

    /// <summary>Text with the padding spaces of the CPU removed; blank is null.</summary>
    private static string? Trimmed(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var text) && text.Trim() is { Length: > 0 } t ? t : null;
}
