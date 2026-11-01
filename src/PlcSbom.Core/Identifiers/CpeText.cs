using System.Text;

namespace PlcSbom.Core.Identifiers;

/// <summary>Attributes of a CPE 2.3 formatted string (after "cpe:2.3:"), where '\' quotes the next character.</summary>
public static class CpeText
{
    /// <summary>Splits at every ':' that is not quoted; the quoting stays in the parts.</summary>
    public static List<string> Split(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                current.Append(text[i]).Append(text[++i]);
            }
            else if (text[i] == ':')
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(text[i]);
            }
        }
        parts.Add(current.ToString());
        return parts;
    }

    /// <summary>"4\.7" → "4.7".</summary>
    public static string Unescape(string part)
    {
        var b = new StringBuilder(part.Length);
        for (var i = 0; i < part.Length; i++)
        {
            if (part[i] == '\\' && i + 1 < part.Length)
            {
                i++;
            }
            b.Append(part[i]);
        }
        return b.ToString();
    }
}
