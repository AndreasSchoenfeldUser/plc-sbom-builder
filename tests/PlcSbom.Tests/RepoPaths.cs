namespace PlcSbom.Tests;

/// <summary>
/// Repository root for tests, found by walking up from the test binaries to PlcSbom.sln.
/// [CallerFilePath] does not work: CI builds with ContinuousIntegrationBuild=true, which maps source
/// paths to /_/ (deterministic build).
/// </summary>
public static class RepoPaths
{
    public static string Root { get; } = Find();

    /// <summary>Absolute path below the repository root, e.g. Of("fixtures", "codesys").</summary>
    public static string Of(params string[] parts) => Path.GetFullPath(Path.Combine([Root, .. parts]));

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PlcSbom.sln")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException($"PlcSbom.sln nicht gefunden oberhalb von {AppContext.BaseDirectory}.");
    }
}
