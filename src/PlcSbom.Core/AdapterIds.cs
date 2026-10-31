namespace PlcSbom.Core;

/// <summary>Adapter identifiers as used in machine.yml and on the command line (CLAUDE.md section 1.1).</summary>
public static class AdapterIds
{
    public const string Codesys = "codesys";
    public const string Studio5000 = "studio5000";
    public const string TwinCat = "twincat";
    public const string TiaAml = "tia-aml";
    public const string TiaPortal = "tia-portal";

    /// <summary>All adapters in implementation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Codesys, Studio5000, TwinCat, TiaAml, TiaPortal];
}
