using System.Text;

namespace OnIT.Smtp.Core.Smtp;

internal static class MimeHeaderParsing
{
    /// <summary>
    /// Parses a header value like: multipart/mixed; boundary="abc123"; charset=utf-8
    /// into ("multipart/mixed", { boundary: abc123, charset: utf-8 }). Parameter names
    /// are lower-cased; values keep their original casing.
    /// </summary>
    public static (string MainValue, Dictionary<string, string> Parameters) ParseHeaderWithParameters(string headerValue)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var segments = SplitRespectingQuotes(headerValue, ';');
        if (segments.Count == 0) return (string.Empty, parameters);

        var mainValue = segments[0].Trim();

        for (var i = 1; i < segments.Count; i++)
        {
            var segment = segments[i].Trim();
            var eq = segment.IndexOf('=');
            if (eq <= 0) continue;

            var key = segment[..eq].Trim().ToLowerInvariant();
            var value = segment[(eq + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }
            parameters[key] = value;
        }

        return (mainValue, parameters);
    }

    private static List<string> SplitRespectingQuotes(string input, char separator)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var c in input)
        {
            if (c == '"') inQuotes = !inQuotes;

            if (c == separator && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
