using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// Loads/saves <see cref="AppConfiguration"/> as JSON on disk. Writes are atomic
/// (write to a temp file, then replace) so a crash or concurrent read never sees
/// a half-written file -- both the service and the config tool touch this file.
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;

    public ConfigStore(string? path = null)
    {
        _path = path ?? ConfigPaths.ConfigFilePath;
    }

    public bool ConfigExists => File.Exists(_path);

    public AppConfiguration Load()
    {
        if (!File.Exists(_path))
        {
            return new AppConfiguration();
        }

        var json = File.ReadAllText(_path);
        var config = JsonSerializer.Deserialize<AppConfiguration>(json, SerializerOptions);
        return config ?? new AppConfiguration();
    }

    public void Save(AppConfiguration config)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, SerializerOptions);

        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(_path))
        {
            File.Replace(tempPath, _path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _path);
        }
    }
}
