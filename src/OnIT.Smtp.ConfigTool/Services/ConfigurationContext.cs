using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.ConfigTool.Services;

/// <summary>
/// Single in-memory working copy of the configuration, shared by every tab's view model.
/// Persists via the same ConfigStore/JSON file the service reads, and (since the file
/// watcher in the service picks up changes) a Save() here is what makes the service apply
/// the change -- either automatically via the file watcher or explicitly via ReloadConfig
/// over the control pipe for immediate effect.
/// </summary>
public sealed class ConfigurationContext
{
    public static ConfigurationContext Instance { get; } = new();

    private readonly ConfigStore _store = new();
    private readonly ISecretProtector _secretProtector = new PortableSecretProtector();

    public AppConfiguration Current { get; private set; } = new();

    public event Action? ConfigurationChanged;

    private ConfigurationContext()
    {
    }

    public void Load()
    {
        ConfigPaths.EnsureDirectoriesExist();
        Current = _store.Load();
        ConfigurationChanged?.Invoke();
    }

    public void Save()
    {
        _store.Save(Current);
        ConfigurationChanged?.Invoke();
    }

    public string ProtectSecret(string plainText) => _secretProtector.Protect(plainText);

    public string UnprotectSecret(string protectedText) => _secretProtector.Unprotect(protectedText);
}
