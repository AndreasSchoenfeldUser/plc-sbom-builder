using System.Security.Cryptography;
using System.Text;
using PlcSbom.Core;
using PlcSbom.Core.Build;

namespace PlcSbom.Tests.Build;

public sealed class ProgramHashAndGlobTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("plc-sbom-hash-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Sha512(string text) => Convert.ToHexStringLower(SHA512.HashData(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void Directory_hash_follows_the_documented_procedure()
    {
        Write("prog/b.st", "B");
        Write("prog/a/z.st", "Z");
        Write("prog/A.st", "groß");
        Directory.CreateDirectory(Path.Combine(_dir, "prog", "leer"));

        var result = ProgramHash.Compute(Path.Combine(_dir, "prog"));

        // Recomputed by hand (docs/regeln-tr03183.md section 9): ordinal order puts "A.st" before "a/z.st".
        var lines = $"A.st\0{Sha512("groß")}\na/z.st\0{Sha512("Z")}\nb.st\0{Sha512("B")}\n";
        Assert.Equal(Sha512(lines), result.Sha512);
        Assert.Equal(3, result.Files);
    }

    [Fact]
    public void File_hash_is_sha512_of_the_content()
    {
        var result = ProgramHash.Compute(Write("Main.st", "PROGRAM MAIN"));

        Assert.Equal(Sha512("PROGRAM MAIN"), result.Sha512);
        Assert.Equal(1, result.Files);
    }

    [Fact]
    public void Line_endings_change_the_hash()
    {
        var lf = ProgramHash.Compute(Write("lf.st", "a\nb\n"));
        var crlf = ProgramHash.Compute(Write("crlf.st", "a\r\nb\r\n"));

        Assert.NotEqual(lf.Sha512, crlf.Sha512);
    }

    [Fact]
    public void Sample_program_hash_is_stable()
    {
        // Pinned value: samples/** is byte-exact (.gitattributes), so Linux and Windows agree.
        var result = ProgramHash.Compute(RepoPaths.Of("samples", "maschine-xy", "programm", "PLC_1"));

        Assert.Equal("00c89308cb1893cfa521f2b20f13a13d7f12f5f03dbab0e51c042fdac1287a6ee087127cf7e37344c947a97893222e5e3802e48120e6c8e91c6608fbadf42804", result.Sha512);
        Assert.Equal(2, result.Files);
    }

    [Fact]
    public void Glob_finds_files_in_one_directory_sorted()
    {
        Write("sbom/b.cdx.json", "{}");
        Write("sbom/a.cdx.json", "{}");
        Write("sbom/c.json", "{}");
        Write("sbom/tief/d.cdx.json", "{}");

        var found = Glob.Find(_dir, "sbom/*.cdx.json");

        Assert.Equal(["a.cdx.json", "b.cdx.json"], found.Select(Path.GetFileName));
    }

    [Fact]
    public void Double_star_spans_directories()
    {
        Write("sbom/a.cdx.json", "{}");
        Write("sbom/x/y/b.cdx.json", "{}");

        Assert.Equal(2, Glob.Find(_dir, "sbom/**/*.cdx.json").Count);
    }

    [Theory]
    [InlineData("sbom/?.cdx.json", 1)]
    [InlineData("sbom/a.cdx.json", 1)]
    [InlineData("sbom/fehlt.cdx.json", 0)]
    [InlineData("fehlt/*.cdx.json", 0)]
    public void Patterns_and_plain_paths(string pattern, int count)
    {
        Write("sbom/a.cdx.json", "{}");
        Write("sbom/ab.cdx.json", "{}");

        Assert.Equal(count, Glob.Find(_dir, pattern).Count);
    }
}
