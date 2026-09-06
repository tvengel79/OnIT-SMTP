using OnIT.Smtp.Core.Mail;

namespace OnIT.Smtp.Core.Smtp;

/// <summary>Turns an SMTP envelope (MAIL FROM/RCPT TO) plus a parsed DATA payload into an OutboundMessage for Graph.</summary>
public static class OutboundMessageBuilder
{
    public static OutboundMessage Build(string mailFrom, IReadOnlyList<string> rcptTo, byte[] dataPayload)
    {
        var root = MimeParser.Parse(dataPayload);
        var subject = DecodeMimeWord(root.GetHeader("Subject") ?? string.Empty);

        var (bodyText, isHtml, attachments) = ExtractContent(root);

        return new OutboundMessage
        {
            From = mailFrom,
            To = rcptTo,
            Subject = subject,
            Body = bodyText,
            IsHtml = isHtml,
            Attachments = attachments
        };
    }

    private static (string Body, bool IsHtml, List<OutboundAttachment> Attachments) ExtractContent(MimeEntity root)
    {
        var attachments = new List<OutboundAttachment>();
        string? plainText = null;
        string? htmlText = null;

        void Walk(MimeEntity entity)
        {
            if (entity.IsMultipart)
            {
                foreach (var child in entity.Children) Walk(child);
                return;
            }

            var (mediaType, _) = entity.GetContentType();

            if (entity.IsAttachment())
            {
                attachments.Add(new OutboundAttachment
                {
                    FileName = entity.GetAttachmentFileName(),
                    ContentType = mediaType,
                    Content = entity.GetDecodedBody()
                });
                return;
            }

            if (mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
            {
                htmlText ??= entity.GetDecodedText();
            }
            else if (mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(mediaType))
            {
                plainText ??= entity.GetDecodedText();
            }
        }

        Walk(root);

        if (htmlText is not null) return (htmlText, true, attachments);
        if (plainText is not null) return (plainText, false, attachments);
        return (string.Empty, false, attachments);
    }

    /// <summary>Decodes RFC 2047 encoded-words in headers, e.g. "=?UTF-8?B?SGVsbG8=?=". Falls back to the raw text.</summary>
    private static string DecodeMimeWord(string value)
    {
        if (!value.Contains("=?")) return value;

        try
        {
            return DecodeEncodedWords(value);
        }
        catch
        {
            return value;
        }
    }

    private static string DecodeEncodedWords(string value)
    {
        var result = value;
        var pattern = new System.Text.RegularExpressions.Regex(@"=\?(?<charset>[^?]+)\?(?<encoding>[BQ])\?(?<text>[^?]*)\?=",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return pattern.Replace(result, match =>
        {
            try
            {
                var charset = match.Groups["charset"].Value;
                var encoding = System.Text.Encoding.GetEncoding(charset);
                var text = match.Groups["text"].Value;

                if (match.Groups["encoding"].Value.Equals("B", StringComparison.OrdinalIgnoreCase))
                {
                    return encoding.GetString(Convert.FromBase64String(text));
                }

                // Q-encoding: underscores are spaces, =XX is a hex byte.
                var qBytes = DecodeQEncoding(text);
                return encoding.GetString(qBytes);
            }
            catch
            {
                return match.Value;
            }
        });
    }

    private static byte[] DecodeQEncoding(string text)
    {
        using var ms = new MemoryStream();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '_')
            {
                ms.WriteByte((byte)' ');
            }
            else if (text[i] == '=' && i + 2 < text.Length)
            {
                var hex = text.Substring(i + 1, 2);
                ms.WriteByte(Convert.ToByte(hex, 16));
                i += 2;
            }
            else
            {
                ms.WriteByte((byte)text[i]);
            }
        }
        return ms.ToArray();
    }
}
