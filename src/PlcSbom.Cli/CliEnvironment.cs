namespace PlcSbom.Cli;

/// <summary>Process state the command line depends on. Injected so tests can run every platform path.</summary>
public sealed record CliEnvironment(bool IsWindows)
{
    public static CliEnvironment FromProcess() => new(OperatingSystem.IsWindows());
}
