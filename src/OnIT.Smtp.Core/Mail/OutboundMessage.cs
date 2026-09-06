namespace OnIT.Smtp.Core.Mail;

public sealed class OutboundAttachment
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
}

/// <summary>
/// A protocol-agnostic representation of an email to send via Graph. Produced either by
/// parsing an inbound SMTP DATA payload (the relay path) or built directly by the config
/// tool's "send a test email" screen.
/// </summary>
public sealed class OutboundMessage
{
    public required string From { get; init; }
    public required IReadOnlyList<string> To { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Bcc { get; init; } = Array.Empty<string>();
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool IsHtml { get; init; }
    public IReadOnlyList<OutboundAttachment> Attachments { get; init; } = Array.Empty<OutboundAttachment>();
}
