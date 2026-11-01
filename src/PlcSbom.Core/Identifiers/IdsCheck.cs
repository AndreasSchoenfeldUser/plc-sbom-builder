using System.Text.Encodings.Web;
using System.Text.Json;
using PlcSbom.Core.Sbom;

namespace PlcSbom.Core.Identifiers;

/// <summary>Result of ids for one component.</summary>
public enum IdsStatus
{
    /// <summary>gefunden: an identifier is in at least one CSAF document.</summary>
    Found,

    /// <summary>nicht gefunden: the component has a usable identifier, no CSAF document names it.</summary>
    NotFound,

    /// <summary>muster: the only identifier is an order number with placeholders (501xxxxx); not comparable.</summary>
    Pattern,

    /// <summary>keine Kennung: neither order number nor CPE; only the bom-ref identifies it.</summary>
    None,
}

/// <summary>A CSAF product that names the component, and through which identifier.</summary>
public sealed record IdsHit(string Document, string ProductId, string ProductName, string? Vendor, string Via, string Identifier, bool Partial);

public sealed record IdsEntry
{
    public required SbomComponent Component { get; init; }

    public required IdsStatus Status { get; init; }

    public string? OrderNumber { get; init; }

    public bool OrderNumberIsPattern { get; init; }

    public string? Vendor { get; init; }

    public string? Cpe { get; init; }

    /// <summary>The CPE table has a row for vendor and order number (null: no table given).</summary>
    public bool? InCpeMap { get; init; }

    public IReadOnlyList<IdsHit> Hits { get; init; } = [];

    public string StatusText => Text(Status);

    public static string Text(IdsStatus status) => status switch
    {
        IdsStatus.Found => "gefunden",
        IdsStatus.NotFound => "nicht gefunden",
        IdsStatus.Pattern => "muster",
        IdsStatus.None => "keine Kennung",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}

public sealed record IdsReport
{
    public required CsafIndex Csaf { get; init; }

    public required IReadOnlyList<IdsEntry> Entries { get; init; }

    public bool CpeMapGiven { get; init; }

    public int Count(IdsStatus status) => Entries.Count(e => e.Status == status);

    /// <summary>Components with an identifier that can be looked up: found plus not found.</summary>
    public int WithIdentifier => Count(IdsStatus.Found) + Count(IdsStatus.NotFound);
}

/// <summary>
/// plc-sbom ids (CLAUDE.md section 6 item 5, hypothesis H4): can each component be found in CSAF
/// advisories or the CPE table? Order numbers are compared without spaces and case; a CSAF model number
/// may be abbreviated or contain '?' and '*' (CSAF 2.0 section 3.1.3.3.3) and then matches as a pattern.
/// Not a vulnerability assessment: versions, product status and vulnerabilities are not evaluated.
/// </summary>
public static class IdsCheck
{
    public static IdsReport Run(SbomView sbom, CsafIndex csaf, CpeMap? cpeMap)
    {
        var entries = new List<IdsEntry>();
        foreach (var component in sbom.Components)
        {
            var orderNumber = component.Own("device:orderNumber");
            var pattern = component.Own("device:orderNumber:pattern") == "true";
            var vendor = component.Own("device:vendor") ?? component.Supplier?.Name;
            var cpe = component.Cpe;
            var hits = new List<IdsHit>();

            if (orderNumber is not null && !pattern)
            {
                foreach (var product in csaf.Products)
                {
                    foreach (var model in product.ModelNumbers)
                    {
                        if (ModelMatches(model, orderNumber, out var partial))
                        {
                            hits.Add(new IdsHit(product.Document, product.ProductId, product.Name, product.Vendor, "model_numbers", model, partial));
                        }
                    }
                }
            }
            if (cpe is not null)
            {
                foreach (var product in csaf.Products.Where(p => p.Cpe is not null && CpeMatch.Matches(p.Cpe, cpe)))
                {
                    hits.Add(new IdsHit(product.Document, product.ProductId, product.Name, product.Vendor, "cpe", product.Cpe!, false));
                }
            }

            bool? inMap = cpeMap is null || orderNumber is null || vendor is null
                ? null
                : cpeMap.Entries.Any(e => e.Vendor == vendor && Wildcard.IsOrderNumberMatch(e.OrderNumber, orderNumber));
            var usable = (orderNumber is not null && !pattern) || cpe is not null;
            var status = hits.Count > 0 ? IdsStatus.Found
                : usable ? IdsStatus.NotFound
                : orderNumber is not null ? IdsStatus.Pattern
                : IdsStatus.None;
            entries.Add(new IdsEntry
            {
                Component = component,
                Status = status,
                OrderNumber = orderNumber,
                OrderNumberIsPattern = pattern,
                Vendor = vendor,
                Cpe = cpe,
                InCpeMap = inMap,
                Hits = hits.Distinct().ToList(),
            });
        }
        return new IdsReport { Csaf = csaf, Entries = entries, CpeMapGiven = cpeMap is not null };
    }

    /// <summary>
    /// CSAF 2.0 3.1.3.3.3: a model number may be abbreviated (it starts at the first character and stops
    /// anywhere) and uses '?' for one and '*' for any number of characters. Spaces and case do not count.
    /// </summary>
    public static bool ModelMatches(string model, string orderNumber, out bool partial)
    {
        var m = model.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        var o = orderNumber.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        partial = false;
        if (m == o)
        {
            return true;
        }
        partial = true;
        return m.Length > 0 && Wildcard.IsMatch(m.EndsWith('*') ? m : m + "*", o);
    }

    /// <summary>Coverage report as JSON (--out), deterministic. Field names are an interface.</summary>
    public static byte[] WriteJson(IdsReport report, string sbomPath)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            w.WriteStartObject();
            w.WriteNumber("format", 1);
            w.WriteString("command", "ids");
            w.WriteString("sbom", sbomPath.Replace('\\', '/'));
            w.WriteStartObject("csaf");
            w.WriteNumber("documents", report.Csaf.Documents.Count);
            w.WriteNumber("products", report.Csaf.Products.Count);
            w.WriteNumber("withModelNumbers", report.Csaf.Products.Count(p => p.ModelNumbers.Count > 0));
            w.WriteNumber("withCpe", report.Csaf.Products.Count(p => p.Cpe is not null));
            w.WriteNumber("skipped", report.Csaf.Warnings.Count);
            w.WriteEndObject();
            w.WriteStartObject("coverage");
            w.WriteNumber("components", report.Entries.Count);
            w.WriteNumber("withIdentifier", report.WithIdentifier);
            w.WriteNumber("found", report.Count(IdsStatus.Found));
            w.WriteNumber("notFound", report.Count(IdsStatus.NotFound));
            w.WriteNumber("pattern", report.Count(IdsStatus.Pattern));
            w.WriteNumber("none", report.Count(IdsStatus.None));
            w.WriteEndObject();
            w.WriteStartArray("components");
            foreach (var e in report.Entries)
            {
                w.WriteStartObject();
                w.WriteString("bomRef", e.Component.BomRef);
                w.WriteString("name", e.Component.Name);
                w.WriteString("status", e.Status switch
                {
                    IdsStatus.Found => "found",
                    IdsStatus.NotFound => "not-found",
                    IdsStatus.Pattern => "pattern",
                    _ => "none",
                });
                if (e.Vendor is not null)
                {
                    w.WriteString("vendor", e.Vendor);
                }
                if (e.OrderNumber is not null)
                {
                    w.WriteString("orderNumber", e.OrderNumber);
                }
                if (e.Cpe is not null)
                {
                    w.WriteString("cpe", e.Cpe);
                }
                if (e.InCpeMap is { } inMap)
                {
                    w.WriteBoolean("inCpeMap", inMap);
                }
                if (e.Hits.Count > 0)
                {
                    w.WriteStartArray("hits");
                    foreach (var hit in e.Hits)
                    {
                        w.WriteStartObject();
                        w.WriteString("document", hit.Document);
                        w.WriteString("productId", hit.ProductId);
                        w.WriteString("product", hit.ProductName);
                        if (hit.Vendor is not null)
                        {
                            w.WriteString("vendor", hit.Vendor);
                        }
                        w.WriteString("via", hit.Via);
                        w.WriteString("identifier", hit.Identifier);
                        w.WriteBoolean("partial", hit.Partial);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }
}
