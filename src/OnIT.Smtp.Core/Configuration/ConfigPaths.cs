namespace OnIT.Smtp.Core.Configuration;

public static class ConfigPaths
{
    private const string RootDirectoryOverrideVariable = "ONITSMTP_HOME";

    /// <summary>
    /// %ProgramData%\OnIT-SMTP on Windows -- the service (LocalSystem/service account) and
    /// the config tool (elevated admin) read/write here so they always agree on state.
    /// The Part 2 Docker/Linux bridge has no ProgramData equivalent, so it points this at a
    /// mounted volume via the ONITSMTP_HOME environment variable instead; Windows deployments
    /// leave that variable unset and keep the default.
    /// </summary>
    public static string RootDirectory =>
        Environment.GetEnvironmentVariable(RootDirectoryOverrideVariable) is { Length: > 0 } overridePath
            ? overridePath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OnIT-SMTP");

    public static string ConfigFilePath => Path.Combine(RootDirectory, "config.json");

    public static string DefaultLogDirectory => Path.Combine(RootDirectory, "logs");

    /// <summary>Self-signed TLS certificate the bridge's remote API listens with -- see RemoteApiCertificateProvider.</summary>
    public static string RemoteApiCertificatePath => Path.Combine(RootDirectory, "remote-api-cert.pfx");

    /// <summary>Bearer token the bridge's remote API requires -- see RemoteApiTokenProvider.</summary>
    public static string RemoteApiTokenPath => Path.Combine(RootDirectory, "remote-api-token.txt");

    /// <summary>
    /// Config tool-only: remote bridges the operator has paired with (host, port, pinned
    /// certificate fingerprint, protected pairing token). Deliberately separate from
    /// config.json -- the Service/Bridge never read this file, it has nothing to do with the
    /// relay configuration they run.
    /// </summary>
    public static string RemoteBridgesFilePath => Path.Combine(RootDirectory, "remote-bridges.json");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DefaultLogDirectory);
    }
}
