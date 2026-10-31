using System.Runtime.CompilerServices;
using System.Text;

namespace PlcSbom.Tests;

/// <summary>
/// Golden files under tests/PlcSbom.Tests/golden/. With PLC_SBOM_UPDATE_GOLDEN=1 the test writes the
/// file instead of comparing; a changed golden file needs a reason in the commit (CLAUDE.md section 14).
/// </summary>
public static class Golden
{
    public static string PathOf(string name) => Path.Combine(Root(), "golden", name);

    // Evaluated inside this file, so the path is this project's directory on every machine that builds it.
    private static string Root([CallerFilePath] string here = "") => Path.GetDirectoryName(here)!;

    public static void AssertMatches(string name, byte[] actual)
    {
        var path = PathOf(name);
        if (Environment.GetEnvironmentVariable("PLC_SBOM_UPDATE_GOLDEN") == "1")
        {
            File.WriteAllBytes(path, actual);
            return;
        }
        Assert.True(File.Exists(path), $"Golden file {path} fehlt. Mit PLC_SBOM_UPDATE_GOLDEN=1 erzeugen.");
        Assert.Equal(Encoding.UTF8.GetString(File.ReadAllBytes(path)), Encoding.UTF8.GetString(actual));
        Assert.Equal(File.ReadAllBytes(path), actual);
    }
}
