using System.Text;

namespace PlcSbom.Core.FieldState;

/// <summary>
/// compare --csv: one line per entry, separator ';', UTF-8 with BOM so Excel in German settings opens it
/// with umlauts, LF line endings, fields with ';', '"' or a line break in quotes (E-023).
/// </summary>
public static class ComparisonCsv
{
    private static readonly string[] Header =
        ["Station", "Position", "Gerät", "Bestellnummer SBOM", "Bestellnummer Feld", "Firmware SBOM", "Firmware Feld", "Ergebnis", "Zuordnung", "Seriennummer Gerät", "Hinweis"];

    public static byte[] Write(Comparison comparison)
    {
        var b = new StringBuilder();
        Line(b, Header);
        foreach (var e in comparison.Entries)
        {
            Line(b,
            [
                e.Station,
                e.Position,
                e.Device,
                e.SbomOrderNumber,
                e.Field?.OrderNumber,
                e.SbomFirmware,
                e.Field?.Firmware,
                e.StatusText,
                e.MatchedBy switch { "position" => "Position", "orderNumber" => "Bestellnummer", _ => null },
                e.Field?.Serial,
                string.Join(" ", e.Notes),
            ]);
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(b.ToString())];
    }

    private static void Line(StringBuilder b, IEnumerable<string?> cells)
    {
        b.AppendJoin(';', cells.Select(Cell)).Append('\n');
    }

    private static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    }
}
