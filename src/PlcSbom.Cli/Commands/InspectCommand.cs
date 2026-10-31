using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlcSbom.Core;
using PlcSbom.Core.Adapters.Codesys.V35;

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
        [AdapterIds.Studio5000] = "M5",
        [AdapterIds.TwinCat] = "M6",
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

        var content = CodesysV35Adapter.ReadFile(path);
        var project = PlcOpenReader.Read(content, path);
        var inventory = new CodesysInventory(
            Format: 1,
            Adapter: AdapterIds.Codesys,
            Source: CodesysV35Adapter.Source,
            File: path,
            Sha256: Convert.ToHexStringLower(SHA256.HashData(content)),
            Project: project);
        await stdout.WriteAsync(JsonSerializer.Serialize(inventory, Json) + "\n").ConfigureAwait(false);
        return ExitCodes.Ok;
    }

    private sealed record CodesysInventory(int Format, string Adapter, string Source, string File, string Sha256, PlcOpenProject Project);
}
