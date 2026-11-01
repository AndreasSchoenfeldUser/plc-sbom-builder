using PlcSbom.Core.Build;

namespace PlcSbom.Cli;

/// <summary>Process state the command line depends on. Injected so tests can run every platform path.</summary>
public sealed record CliEnvironment(bool IsWindows)
{
    /// <summary>Environment variables, clock and git for build (commit, timestamp).</summary>
    public IBuildEnvironment Build { get; init; } = SystemBuildEnvironment.Instance;

    public static CliEnvironment FromProcess() => new(OperatingSystem.IsWindows());
}
