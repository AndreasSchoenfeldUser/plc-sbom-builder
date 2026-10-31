using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.Codesys.V35;
using PlcSbom.Core.Adapters.Studio5000;
using PlcSbom.Core.Adapters.TwinCat;

namespace PlcSbom.Cli.Commands;

/// <summary>plc-sbom inspect &lt;projektdatei&gt; --adapter &lt;id&gt; (CLAUDE.md section 7.1): raw inventory as JSON.</summary>
public static class InspectCommand
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Milestone of each adapter whose inspect is not built yet.</summary>
    private static readonly Dictionary<string, string> Pending = new(StringComparer.Ordinal)
    {
        [AdapterIds.TiaAml] = "M7",
        [AdapterIds.TiaPortal] = "M14",
    };

    public static async Task<int> RunAsync(string path, string adapter, string? gsdml, TextWriter stdout)
    {
        if (Pending.TryGetValue(adapter, out var milestone))
        {
            throw new UsageException(
                "NOT_IMPLEMENTED",
                $"„inspect“ für den Adapter {adapter} ist in plc-sbom {ToolVersion.Short} noch nicht umgesetzt. Das folgt mit Meilenstein {milestone}.");
        }
        if (gsdml is not null)
        {
            throw new UsageException("USAGE", $"--gsdml gilt nur für die Adapter {AdapterIds.TiaAml} und {AdapterIds.TiaPortal}.");
        }

        string json;
        if (adapter == AdapterIds.TwinCat)
        {
            if (!File.Exists(path))
            {
                throw new UsageException("SOURCE_NOT_FOUND", $"Die Quelldatei {path} gibt es nicht.");
            }
            var content = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            json = JsonSerializer.Serialize(new TwinCatInventory(
                Format: 1,
                Adapter: AdapterIds.TwinCat,
                File: path,
                Sha256: Convert.ToHexStringLower(SHA256.HashData(content)),
                Project: TwinCatReader.Read(path, content, path)), Json);
        }
        else if (adapter == AdapterIds.Studio5000)
        {
            var content = Studio5000Adapter.ReadFile(path);
            json = JsonSerializer.Serialize(new Studio5000Inventory(
                Format: 1,
                Adapter: AdapterIds.Studio5000,
                File: path,
                Sha256: Convert.ToHexStringLower(SHA256.HashData(content)),
                Project: L5xReader.Read(content, path)), Json);
        }
        else
        {
            var content = CodesysV35Adapter.ReadFile(path);
            json = JsonSerializer.Serialize(new CodesysInventory(
                Format: 1,
                Adapter: AdapterIds.Codesys,
                Source: CodesysV35Adapter.Source,
                File: path,
                Sha256: Convert.ToHexStringLower(SHA256.HashData(content)),
                Project: PlcOpenReader.Read(content, path)), Json);
        }
        await stdout.WriteAsync(json + "\n").ConfigureAwait(false);
        return ExitCodes.Ok;
    }

    private sealed record CodesysInventory(int Format, string Adapter, string Source, string File, string Sha256, PlcOpenProject Project);

    private sealed record Studio5000Inventory(int Format, string Adapter, string File, string Sha256, L5xProject Project);

    private sealed record TwinCatInventory(int Format, string Adapter, string File, string Sha256, TwinCatProject Project);
}
