using System.Runtime.CompilerServices;

namespace PlcSbom.Tests;

/// <summary>Files under fixtures/ (origin and licence in fixtures/FIXTURES.md).</summary>
public static class Fixtures
{
    /// <summary>Absolute path of a fixture, e.g. "codesys/v35/robin-codesys_project.xml".</summary>
    public static string PathOf(string relative) => Path.GetFullPath(Path.Combine(Root(), relative));

    public static byte[] Read(string relative) => File.ReadAllBytes(PathOf(relative));

    private static string Root([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "fixtures");
}
