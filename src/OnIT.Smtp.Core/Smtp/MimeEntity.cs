namespace OnIT.Smtp.Core.Smtp;

/// <summary>A single MIME entity: either a leaf part with a body, or a multipart container with children.</summary>
public sealed class MimeEntity
{
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] RawBody { get; set; } = Array.Empty<byte>();
    public List<MimeEntity> Children { get; } = new();

    public string? GetHeader(string name) => Headers.TryGetValue(name, out var value) ? value : null;

    public (string MediaType, Dictionary<string, string> Parameters) GetContentType()
    {
        var raw = GetHeader("Content-Type") ?? "text/plain";
        return MimeHeaderParsing.ParseHeaderWithParameters(raw);
    }

    public string GetTransferEncoding() => GetHeader("Content-Transfer-Encoding")?.Trim().ToLowerInvariant() ?? "7bit";

    public bool IsMultipart => GetContentType().MediaType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase);

    public bool IsAttachment()
    {
        var disposition = GetHeader("Content-Disposition");
        if (disposition is not null && disposition.TrimStart().StartsWith("attachment", StringComparison.OrdinalIgnoreCase))
            return true;

        var (mediaType, _) = GetContentType();
        // A part with no filename and a text/* media type is body text, not an attachment.
        var (_, dispositionParams) = disposition is null
            ? (string.Empty, new Dictionary<string, string>())
            : MimeHeaderParsing.ParseHeaderWithParameters(disposition);
        var (_, contentTypeParams) = GetContentType();
        var hasFileName = dispositionParams.ContainsKey("filename") || contentTypeParams.ContainsKey("name");

        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) && !hasFileName)
            return false;

        return hasFileName || !mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) && !IsMultipart;
    }

    public string GetAttachmentFileName()
    {
        var disposition = GetHeader("Content-Disposition");
        if (disposition is not null)
        {
            var (_, dispositionParams) = MimeHeaderParsing.ParseHeaderWithParameters(disposition);
            if (dispositionParams.TryGetValue("filename", out var fileName) && !string.IsNullOrWhiteSpace(fileName))
                return fileName;
        }

        var (_, contentTypeParams) = GetContentType();
        if (contentTypeParams.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            return name;

        return "attachment.bin";
    }

    public byte[] GetDecodedBody() => MimeTransferEncoding.Decode(RawBody, GetTransferEncoding());

    public string GetDecodedText()
    {
        var (_, parameters) = GetContentType();
        var charset = parameters.TryGetValue("charset", out var cs) ? cs : "utf-8";
        var bytes = GetDecodedBody();
        try
        {
            var encoding = System.Text.Encoding.GetEncoding(charset);
            return encoding.GetString(bytes);
        }
        catch (ArgumentException)
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
}
