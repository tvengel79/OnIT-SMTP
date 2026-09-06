namespace OnIT.Smtp.Core.Smtp;

public sealed class SmtpTransaction
{
    public required string ClientAddress { get; init; }
    public required string MailFrom { get; init; }
    public required IReadOnlyList<string> RcptTo { get; init; }
    public required byte[] Data { get; init; }
}

public sealed class SmtpDeliveryResult
{
    public bool Accepted { get; init; }

    /// <summary>Human-readable reason surfaced in the SMTP rejection response and logs.</summary>
    public string? RejectReason { get; init; }

    public static SmtpDeliveryResult Accept() => new() { Accepted = true };
    public static SmtpDeliveryResult Reject(string reason) => new() { Accepted = false, RejectReason = reason };
}
