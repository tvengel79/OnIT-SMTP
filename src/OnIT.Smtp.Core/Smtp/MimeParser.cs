using System.Text;

namespace OnIT.Smtp.Core.Smtp;

/// <summary>
/// A pragmatic MIME parser covering what LAN devices/apps typically send to a local relay:
/// simple text/plain or text/html messages, and multipart/mixed|alternative|related with
/// file attachments (e.g. scan-to-email PDFs). Not a full RFC 2045-2049 implementation --
/// malformed or exotic input degrades to "whole payload as the body" rather than throwing.
/// </summary>
public static class MimeParser
{
    public static MimeEntity Parse(byte[] rawMessage)
    {
        var (headers, bodyStart) = ParseHeaders(rawMessage, 0);
        var entity = new MimeEntity();
        foreach (var (key, value) in headers) entity.Headers[key] = value;

        var bodyBytes = rawMessage[bodyStart..];

        if (entity.IsMultipart)
        {
            var (_, parameters) = entity.GetContentType();
            if (parameters.TryGetValue("boundary", out var boundary) && !string.IsNullOrEmpty(boundary))
            {
                foreach (var partBytes in SplitOnBoundary(bodyBytes, boundary))
                {
                    entity.Children.Add(Parse(partBytes));
                }
                return entity;
            }
        }

        entity.RawBody = bodyBytes;
        return entity;
    }

    private static (List<(string Key, string Value)> Headers, int BodyStartOffset) ParseHeaders(byte[] data, int offset)
    {
        var headers = new List<(string, string)>();
        var text = Encoding.ASCII.GetString(data, offset, data.Length - offset);

        var pos = 0;
        string? currentKey = null;
        var currentValue = new StringBuilder();

        while (pos < text.Length)
        {
            var eol = text.IndexOf('\n', pos);
            var lineEnd = eol == -1 ? text.Length : eol;
            var line = text[pos..lineEnd].TrimEnd('\r');

            if (line.Length == 0)
            {
                // Blank line ends the header block.
                if (currentKey is not null) headers.Add((currentKey, currentValue.ToString().Trim()));
                var headerByteLength = Encoding.ASCII.GetByteCount(text[..(eol == -1 ? text.Length : eol + 1)]);
                return (headers, offset + headerByteLength);
            }

            if ((line[0] == ' ' || line[0] == '\t') && currentKey is not null)
            {
                // Folded header continuation line.
                currentValue.Append(' ').Append(line.Trim());
            }
            else
            {
                if (currentKey is not null) headers.Add((currentKey, currentValue.ToString().Trim()));

                var colon = line.IndexOf(':');
                if (colon > 0)
                {
                    currentKey = line[..colon];
                    currentValue = new StringBuilder(line[(colon + 1)..]);
                }
                else
                {
                    currentKey = null;
                    currentValue = new StringBuilder();
                }
            }

            pos = eol == -1 ? text.Length : eol + 1;
        }

        if (currentKey is not null) headers.Add((currentKey, currentValue.ToString().Trim()));
        return (headers, data.Length);
    }

    private static IEnumerable<byte[]> SplitOnBoundary(byte[] body, string boundary)
    {
        var delimiter = Encoding.ASCII.GetBytes("--" + boundary);
        var parts = new List<byte[]>();

        var positions = new List<int>();
        var searchFrom = 0;
        while (true)
        {
            var idx = IndexOf(body, delimiter, searchFrom);
            if (idx == -1) break;
            positions.Add(idx);
            searchFrom = idx + delimiter.Length;
        }

        for (var i = 0; i < positions.Count - 1; i++)
        {
            var partStart = positions[i] + delimiter.Length;
            // Skip the CRLF right after the boundary marker.
            if (partStart < body.Length && body[partStart] == '\r') partStart++;
            if (partStart < body.Length && body[partStart] == '\n') partStart++;

            var partEnd = positions[i + 1];
            if (partEnd > partStart)
            {
                var length = partEnd - partStart;
                // Trim the trailing CRLF before the next boundary marker.
                if (length >= 2 && body[partStart + length - 2] == '\r' && body[partStart + length - 1] == '\n') length -= 2;
                else if (length >= 1 && body[partStart + length - 1] == '\n') length -= 1;

                if (length > 0)
                {
                    var slice = new byte[length];
                    Array.Copy(body, partStart, slice, 0, length);
                    parts.Add(slice);
                }
            }
        }

        return parts;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int startFrom)
    {
        if (needle.Length == 0 || startFrom >= haystack.Length) return -1;

        for (var i = startFrom; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
