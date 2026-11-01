using System.Security.Cryptography;
using System.Text;

namespace PlcSbom.Core.Build;

/// <summary>SHA-512 over the sources of a user program and the number of files it covers.</summary>
public sealed record ProgramHashResult(string Sha512, int Files);

/// <summary>
/// Hash over the program sources (CLAUDE.md section 5.6, docs/regeln-tr03183.md section 9), as in
/// ../plc-build-tool/docs/adr/0005-hash-ueber-quellen.md but with SHA-512:
/// file → SHA-512 of the content; directory → all files recursively, path relative with '/', sorted
/// ordinal, one line "&lt;path&gt;\0&lt;sha512&gt;\n" per file, SHA-512 over the UTF-8 bytes of all lines.
/// The hash is over the bytes as checked out: line endings changed by Git (core.autocrlf) change it.
/// </summary>
public static class ProgramHash
{
    public static ProgramHashResult Compute(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            return new ProgramHashResult(Hex(SHA512.HashData(File.ReadAllBytes(fullPath))), 1);
        }
        if (!Directory.Exists(fullPath))
        {
            throw new FileNotFoundException(fullPath);
        }
        var files = Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
            .Select(f => (Full: f, Relative: Path.GetRelativePath(fullPath, f).Replace('\\', '/')))
            .OrderBy(f => f.Relative, StringComparer.Ordinal)
            .ToList();
        var lines = new StringBuilder();
        foreach (var (full, relative) in files)
        {
            lines.Append(relative).Append('\0').Append(Hex(SHA512.HashData(File.ReadAllBytes(full)))).Append('\n');
        }
        return new ProgramHashResult(Hex(SHA512.HashData(Encoding.UTF8.GetBytes(lines.ToString()))), files.Count);
    }

    private static string Hex(byte[] hash) => Convert.ToHexStringLower(hash);
}
