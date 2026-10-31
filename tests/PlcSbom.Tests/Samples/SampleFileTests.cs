using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PlcSbom.Tests.Samples;

/// <summary>
/// The minimal SBOM is also kept as a file, so the sbomqs cross-check (tools/crosscheck-sbomqs.cs) sees a
/// component with file name, hash and file properties present, which the hand model never has.
/// </summary>
public sealed class SampleFileTests
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public void Minimal_sbom_matches_its_golden_file()
    {
        var json = MinimalSbom.Create().ToJsonString(Indented) + "\n";

        Golden.AssertMatches("minimal-synthetisch.cdx.json", Encoding.UTF8.GetBytes(json));
    }
}
