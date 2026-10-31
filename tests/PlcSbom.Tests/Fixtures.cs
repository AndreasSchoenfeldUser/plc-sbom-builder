namespace PlcSbom.Tests;

/// <summary>Files under fixtures/ (origin and licence in fixtures/FIXTURES.md).</summary>
public static class Fixtures
{
    /// <summary>Absolute path of a fixture, e.g. "codesys/v35/robin-codesys_project.xml".</summary>
    public static string PathOf(string relative) => RepoPaths.Of("fixtures", relative);

    public static byte[] Read(string relative) => File.ReadAllBytes(PathOf(relative));
}
