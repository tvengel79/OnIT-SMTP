using System.IO;
using System.Text.Json;
using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.ConfigTool.Services;

/// <summary>Loads/saves the paired remote bridges list as JSON, mirroring ConfigStore's atomic-write pattern.</summary>
public sealed class RemoteBridgeStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _path;

    public RemoteBridgeStore(string? path = null)
    {
        _path = path ?? ConfigPaths.RemoteBridgesFilePath;
    }

    public List<RemoteBridgeConnection> Load()
    {
        if (!File.Exists(_path)) return new List<RemoteBridgeConnection>();

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<RemoteBridgeConnection>>(json, SerializerOptions) ?? new List<RemoteBridgeConnection>();
    }

    public void Save(IReadOnlyList<RemoteBridgeConnection> bridges)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(bridges, SerializerOptions);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(_path)) File.Replace(tempPath, _path, destinationBackupFileName: null);
        else File.Move(tempPath, _path);
    }
}
