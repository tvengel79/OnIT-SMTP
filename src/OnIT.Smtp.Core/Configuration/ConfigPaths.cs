namespace OnIT.Smtp.Core.Configuration;

public static class ConfigPaths
{
    /// <summary>
    /// %ProgramData%\OnIT-SMTP on Windows. Both the service (LocalSystem/service account)
    /// and the config tool (elevated admin) read/write here so they always agree on state.
    /// </summary>
    public static string RootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OnIT-SMTP");

    public static string ConfigFilePath => Path.Combine(RootDirectory, "config.json");

    public static string DefaultLogDirectory => Path.Combine(RootDirectory, "logs");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DefaultLogDirectory);
    }
}
