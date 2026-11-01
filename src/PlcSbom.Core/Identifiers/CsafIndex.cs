using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlcSbom.Core.Identifiers;

/// <summary>A product of a CSAF document with an identification helper.</summary>
public sealed record CsafProduct(
    string Document,
    string ProductId,
    string Name,
    string? Vendor,
    IReadOnlyList<string> ModelNumbers,
    string? Cpe);

/// <summary>A CSAF document read from the directory.</summary>
public sealed record CsafDocument(string Path, string TrackingId, string? Title, string? Publisher, int Products);

/// <summary>
/// CSAF 2.0 documents from a local directory (CLAUDE.md section 6), read without network. Only the
/// product tree is read: product_identification_helper with model_numbers and cpe, in branches and in
/// full_product_names. Backed by Siemens ProductCERT documents (model_numbers) and the BSI example of the
/// CSAF specification (cpe), see fixtures/FIXTURES.md. Vulnerabilities, scores and remediations are not
/// read: ids tests identifiers, it does not assess vulnerabilities.
/// </summary>
public sealed class CsafIndex
{
    private CsafIndex(IReadOnlyList<CsafDocument> documents, IReadOnlyList<CsafProduct> products, IReadOnlyList<(string Code, string Message)> warnings)
    {
        Documents = documents;
        Products = products;
        Warnings = warnings;
    }

    public IReadOnlyList<CsafDocument> Documents { get; }

    public IReadOnlyList<CsafProduct> Products { get; }

    /// <summary>Files that are no CSAF document or not readable; they are skipped.</summary>
    public IReadOnlyList<(string Code, string Message)> Warnings { get; }

    /// <summary>All *.json below <paramref name="directory"/>, sorted by path; display paths relative to it.</summary>
    public static CsafIndex Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new UsageException("CSAF_DIR_NOT_FOUND", $"Das CSAF-Verzeichnis {directory} gibt es nicht.");
        }
        var files = Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            .Select(f => (Full: f, Display: Path.GetRelativePath(directory, f).Replace('\\', '/')))
            .OrderBy(f => f.Display, StringComparer.Ordinal)
            .ToList();
        return Read(files.Select(f => (f.Display, (Func<byte[]>)(() => File.ReadAllBytes(f.Full)))));
    }

    internal static CsafIndex Read(IEnumerable<(string Display, Func<byte[]> Read)> files)
    {
        var documents = new List<CsafDocument>();
        var products = new List<CsafProduct>();
        var warnings = new List<(string, string)>();
        foreach (var (display, read) in files)
        {
            JsonObject? root;
            try
            {
                root = JsonNode.Parse(read()) as JsonObject;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                warnings.Add(("CSAF_INVALID", $"{display} ist nicht lesbar oder kein gültiges JSON und wird übersprungen: {ex.Message}"));
                continue;
            }
            if (root?["document"] is not JsonObject document || Text(document["csaf_version"]) is null)
            {
                warnings.Add(("CSAF_NOT_CSAF", $"{display} ist kein CSAF-Dokument (document.csaf_version fehlt) und wird übersprungen."));
                continue;
            }
            var id = Text(document["tracking"]?["id"]) ?? display;
            var before = products.Count;
            Walk(root["product_tree"]?["branches"] as JsonArray, null, id, products);
            foreach (var full in (root["product_tree"]?["full_product_names"] as JsonArray ?? []).OfType<JsonObject>())
            {
                Add(full, null, id, products);
            }
            documents.Add(new CsafDocument(display, id, Text(document["title"]), Text(document["publisher"]?["name"]), products.Count - before));
        }
        return new CsafIndex(documents, products, warnings);
    }

    private static void Walk(JsonArray? branches, string? vendor, string document, List<CsafProduct> products)
    {
        foreach (var branch in (branches ?? []).OfType<JsonObject>())
        {
            var own = Text(branch["category"]) == "vendor" ? Text(branch["name"]) ?? vendor : vendor;
            if (branch["product"] is JsonObject product)
            {
                Add(product, own, document, products);
            }
            Walk(branch["branches"] as JsonArray, own, document, products);
        }
    }

    private static void Add(JsonObject product, string? vendor, string document, List<CsafProduct> products)
    {
        if (product["product_identification_helper"] is not JsonObject helper)
        {
            return;
        }
        var models = (helper["model_numbers"] as JsonArray ?? []).Select(Text).OfType<string>().Where(m => m.Trim().Length > 0).ToList();
        var cpe = Text(helper["cpe"]);
        if (models.Count == 0 && cpe is null)
        {
            return;
        }
        products.Add(new CsafProduct(document, Text(product["product_id"]) ?? "", Text(product["name"]) ?? "", vendor, models, cpe));
    }

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

/// <summary>
/// CPE comparison for ids: CPE 2.3 formatted strings and CPE 2.2 URIs (both allowed in CSAF 2.0,
/// section 3.1.3.3.1), attribute by attribute. "*" or a missing attribute on the CSAF side matches
/// anything, "-" only "-"; case does not count. A simplified form of CPE name matching (NISTIR 7696):
/// wildcards inside a value are not evaluated (E-024).
/// </summary>
public static class CpeMatch
{
    public static bool Matches(string csafCpe, string sbomCpe)
    {
        var pattern = Attributes(csafCpe);
        var name = Attributes(sbomCpe);
        if (pattern is null || name is null)
        {
            return false;
        }
        for (var i = 0; i < pattern.Count; i++)
        {
            var p = pattern[i];
            if (p is "*" or "")
            {
                continue;
            }
            var n = i < name.Count ? name[i] : "*";
            if (!string.Equals(p, n, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>part, vendor, product, version, update, edition, language, … without escaping.</summary>
    internal static List<string>? Attributes(string cpe)
    {
        if (cpe.StartsWith("cpe:2.3:", StringComparison.OrdinalIgnoreCase))
        {
            return CpeText.Split(cpe["cpe:2.3:".Length..]).Select(CpeText.Unescape).ToList();
        }
        if (cpe.StartsWith("cpe:/", StringComparison.OrdinalIgnoreCase))
        {
            return cpe["cpe:/".Length..].Split(':').Select(a => Uri.UnescapeDataString(a)).ToList();
        }
        return null;
    }
}
