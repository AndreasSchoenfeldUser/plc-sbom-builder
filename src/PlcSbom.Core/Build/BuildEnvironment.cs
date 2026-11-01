using System.ComponentModel;
using System.Diagnostics;

namespace PlcSbom.Core.Build;

/// <summary>
/// Process state the build depends on: environment variables, the clock and Git. Injected so tests
/// control commit and timestamp (CLAUDE.md section 8.4).
/// </summary>
public interface IBuildEnvironment
{
    string? Variable(string name);

    DateTimeOffset Now { get; }

    /// <summary>Runs git in <paramref name="workingDirectory"/>; trimmed stdout, or null if git is missing or fails.</summary>
    string? Git(string workingDirectory, params string[] arguments);
}

/// <summary>The real process: environment, system clock, git from PATH.</summary>
public sealed class SystemBuildEnvironment : IBuildEnvironment
{
    public static SystemBuildEnvironment Instance { get; } = new();

    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    public string? Variable(string name) => Environment.GetEnvironmentVariable(name);

    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    public string? Git(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GitTimeout))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
            return process.ExitCode == 0 ? output.Result.Trim() : null;
        }
        catch (Win32Exception)
        {
            // git is not installed or not on PATH.
            return null;
        }
    }
}
