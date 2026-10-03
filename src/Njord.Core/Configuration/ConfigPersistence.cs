using System.Text.Json;

namespace Njord.Configuration;

public sealed class ConfigPersistence
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ConfigPersistence(string basePath = "data")
    {
        _filePath = Path.Combine(basePath, "njord-config.json");
    }

    public async Task SaveAsync(NjordOptions options)
    {
        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    // The read happens via .NET's ConfigurationBuilder.AddJsonFile("njord-config.json", optional: true, reloadOnChange: true)
    // So we don't need a ReadAsync here — IOptionsMonitor handles it.
}
