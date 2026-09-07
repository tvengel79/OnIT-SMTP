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

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DefaultLogDirectory);
    }
}
