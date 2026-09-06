namespace OnIT.Smtp.Core.Configuration;

public sealed class SmtpListenerSettings
{
    /// <summary>Interface to bind on. "0.0.0.0" for all interfaces (still gated by the IP allow list).</summary>
    public string BindAddress { get; set; } = "0.0.0.0";

    public int Port { get; set; } = 25;

    /// <summary>Maximum message size in bytes. 0 = unlimited.</summary>
    public int MaxMessageSizeBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Idle session timeout in seconds.</summary>
    public int SessionTimeoutSeconds { get; set; } = 120;

    public string ServerHostName { get; set; } = Environment.MachineName;
}

public sealed class AdvancedSettings
{
    /// <summary>
    /// When true, the MAIL FROM address on every relayed message must match one of the
    /// enabled entries in AllowedSenders, resolved against Entra mailbox users. When false,
    /// any MAIL FROM is accepted (still subject to the IP allow list) and Graph itself decides
    /// whether the app is allowed to send as that address.
    /// </summary>
    public bool RestrictToAllowedSenders { get; set; } = true;
}
