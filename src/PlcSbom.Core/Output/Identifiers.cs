using System.Security.Cryptography;
using System.Text;

namespace PlcSbom.Core.Output;

/// <summary>Readable, stable bom-refs (CLAUDE.md section 4.3).</summary>
public static class BomRef
{
    /// <summary>Lower-case ASCII slug: letters and digits, everything else becomes '-'; umlauts are transliterated.</summary>
    public static string Slug(string text)
    {
        var b = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            switch (c)
            {
                case 'ä': b.Append("ae"); break;
                case 'ö': b.Append("oe"); break;
                case 'ü': b.Append("ue"); break;
                case 'ß': b.Append("ss"); break;
                case >= 'a' and <= 'z' or >= '0' and <= '9': b.Append(c); break;
                default:
                    if (b.Length > 0 && b[^1] != '-')
                    {
                        b.Append('-');
                    }
                    break;
            }
        }
        var slug = b.ToString().Trim('-');
        return slug.Length == 0 ? "x" : slug;
    }

    /// <summary>Hands out unique refs in call order: the second "a" becomes "a-2".</summary>
    public sealed class Registry
    {
        private readonly HashSet<string> used = new(StringComparer.Ordinal);

        public string Claim(string candidate)
        {
            var result = candidate;
            for (var n = 2; !used.Add(result); n++)
            {
                result = $"{candidate}-{n}";
            }
            return result;
        }
    }
}

/// <summary>Name-based UUID version 5 (RFC 9562 section 5.5) for the SBOM serial number.</summary>
public static class Uuid5
{
    /// <summary>Namespace of plc-sbom serial numbers. Fixed; changing it changes every serial number.</summary>
    public static readonly Guid Namespace = new("6f1c2a43-8d0e-5b7a-9c3e-2f4d6a8b0c1e");

    public static Guid Create(Guid ns, string name)
    {
        var nsBytes = ns.ToByteArray(bigEndian: true);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[nsBytes.Length + nameBytes.Length];
        nsBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, nsBytes.Length);
#pragma warning disable CA5350 // SHA-1 is what RFC 9562 prescribes for version 5, not used for security.
        var hash = SHA1.HashData(input);
#pragma warning restore CA5350
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
