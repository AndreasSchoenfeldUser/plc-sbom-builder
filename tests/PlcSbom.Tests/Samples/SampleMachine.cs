using System.Text;
using PlcSbom.Core.Build;
using PlcSbom.Core.Config;
using PlcSbom.Core.FieldState;
using PlcSbom.Core.Output;
using PlcSbom.Core.Output.Report;
using PlcSbom.Core.Sbom;
using PlcSbom.Core.Validation;
using PlcSbom.Tests.Build;

namespace PlcSbom.Tests.Samples;

/// <summary>
/// The sample machine samples/maschine-xy built once, as build does it: SBOM (equal to the golden file
/// maschine-xy.cdx.json), validation with the findings of the run, report extras and the field file 1042.
/// </summary>
public static class SampleMachine
{
    /// <summary>Path of the SBOM as reports and result files name it.</summary>
    public const string SbomPath = "build/sbom-maschine.cdx.json";

    public static string FieldPath => RepoPaths.Of("samples", "maschine-xy", "feld", "1042.json");

    private static readonly Lazy<Task<Built>> Instance = new(Create);

    public sealed record Built(BuildOutcome Outcome, byte[] Sbom, ValidationReport Validation)
    {
        public string Json => Encoding.UTF8.GetString(Sbom);

        public SbomView View() => SbomView.Parse(Json);

        public ReportExtras Extras => ReportExtras.From(Outcome.Machine, Outcome.IgnoredDevices);
    }

    public static Task<Built> Get() => Instance.Value;

    /// <summary>The field file with the path the golden files show (relative to the repository).</summary>
    public static AsFoundState Field() =>
        FieldStateReader.Parse(File.ReadAllBytes(FieldPath), "samples/maschine-xy/feld/1042.json");

    private static async Task<Built> Create()
    {
        var outcome = await MachineBuilder.BuildAsync(
            new BuildRequest
            {
                Config = MachineConfigLoader.Load(RepoPaths.Of("samples", "maschine-xy", "machine.yml")),
                Release = "R2026.10",
                Commit = MachineBuilderTests.Commit,
                RepositoryRoot = RepoPaths.Root,
            },
            FakeBuildEnvironment.Reproducible());
        var sbom = CycloneDxWriter.Write(outcome.Machine, new WriterOptions { Namespace = "firma", Timestamp = outcome.Timestamp, ToolVersion = "0.1.0" });
        var validation = (await SbomValidator.ValidateAsync(Encoding.UTF8.GetString(sbom))).WithFindings(outcome.Findings);
        return new Built(outcome, sbom, validation);
    }
}
