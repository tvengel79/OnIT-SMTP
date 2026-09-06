using System.Text;

namespace OnIT.Smtp.Core.Smtp;

internal static class MimeTransferEncoding
{
    public static byte[] Decode(byte[] raw, string transferEncoding) => transferEncoding switch
    {
        "base64" => DecodeBase64(raw),
        "quoted-printable" => DecodeQuotedPrintable(raw),
        _ => raw // 7bit, 8bit, binary -- already the literal bytes.
    };

    private static byte[] DecodeBase64(byte[] raw)
    {
        var text = Encoding.ASCII.GetString(raw);
        // Strip whitespace/newlines -- devices commonly wrap base64 at 76 columns.
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c)) sb.Append(c);
        }

        var cleaned = sb.ToString();
        // Be lenient about missing padding from misbehaving senders.
        var remainder = cleaned.Length % 4;
        if (remainder != 0) cleaned = cleaned.PadRight(cleaned.Length + (4 - remainder), '=');

        try
        {
            return Convert.FromBase64String(cleaned);
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }

    private static byte[] DecodeQuotedPrintable(byte[] raw)
    {
        using var output = new MemoryStream();
        var i = 0;
        while (i < raw.Length)
        {
            var b = raw[i];
            if (b == '=' && i + 1 < raw.Length)
            {
                // Soft line break: "=\r\n" or "=\n" -- consume and continue, no output byte.
                if (raw[i + 1] == '\n')
                {
                    i += 2;
                    continue;
                }
                if (i + 2 < raw.Length && raw[i + 1] == '\r' && raw[i + 2] == '\n')
                {
                    i += 3;
                    continue;
                }

                if (i + 2 < raw.Length && IsHexDigit(raw[i + 1]) && IsHexDigit(raw[i + 2]))
                {
                    var hex = Encoding.ASCII.GetString(raw, i + 1, 2);
                    output.WriteByte(Convert.ToByte(hex, 16));
                    i += 3;
                    continue;
                }
            }

            output.WriteByte(b);
            i++;
        }
        return output.ToArray();
    }

    private static bool IsHexDigit(byte b) =>
        (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'A' && b <= (byte)'F') || (b >= (byte)'a' && b <= (byte)'f');
}
