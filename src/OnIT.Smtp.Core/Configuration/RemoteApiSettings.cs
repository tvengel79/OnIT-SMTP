namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// Optional HTTPS API a host (currently only OnIT.Smtp.Bridge) can expose so a config tool on
/// another machine can push a new configuration and read live status without copying files
/// onto the host by hand. Off by default -- enabling it adds a network-facing endpoint, so an
/// operator opts in deliberately. Authentication is a bearer token generated locally on first
/// boot plus TLS pinned to a self-signed certificate's fingerprint; both live next to
/// secret.key under the same config root. See OnIT.Smtp.Core.RemoteApi.
/// </summary>
public sealed class RemoteApiSettings
{
    public bool Enabled { get; set; }
    public int Port { get; set; } = 8443;
}
