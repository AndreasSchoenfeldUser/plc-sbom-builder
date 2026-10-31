namespace PlcSbom.Core;

/// <summary>
/// Exit codes of plc-sbom (CLAUDE.md section 7.2). They are an interface for pipelines and
/// are only changed with an entry in docs/entscheidungen.md.
/// </summary>
public static class ExitCodes
{
    /// <summary>SBOM valid; warnings in a development build and notes do not change this.</summary>
    public const int Ok = 0;

    /// <summary>Finding: schema error, required field missing, warnings with --release-build, compare deviations.</summary>
    public const int Finding = 1;

    /// <summary>Call or configuration wrong: machine.yml, source file, adapter id.</summary>
    public const int Usage = 2;

    /// <summary>Environment: TIA missing, Openness group, firewall, license, Openness command on Linux.</summary>
    public const int Environment = 3;

    /// <summary>Abort: timeout, worker crashed or without valid response, unexpected error.</summary>
    public const int Aborted = 4;
}
