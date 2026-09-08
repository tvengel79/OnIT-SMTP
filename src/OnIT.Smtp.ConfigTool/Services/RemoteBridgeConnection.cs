namespace OnIT.Smtp.ConfigTool.Services;

/// <summary>
/// A Part 2 Docker bridge the config tool has been paired with: enough to reach its remote
/// API (host/port), pin its self-signed certificate (fingerprint, read once from the bridge's
/// boot log), and authenticate (pairing token, also read from that log -- stored encrypted at
/// rest via the same ISecretProtector used for the Entra client secret, never in plain text).
/// </summary>
public sealed class RemoteBridgeConnection
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 8443;
    public string CertificateFingerprint { get; set; } = string.Empty;
    public string ProtectedPairingToken { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? $"{Host}:{Port}" : $"{Name} ({Host}:{Port})";
}
