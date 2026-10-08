using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class WritableNjordOptions(
    IOptionsMonitor<NjordOptions> optionsMonitor,
    IConfigurationRoot configurationRoot,
    string overridePath) : IWritableOptions<NjordOptions>
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);

    public NjordOptions Value => optionsMonitor.CurrentValue;

    public NjordOptions Update(Action<NjordOptions> applyChanges)
    {
        _lock.Wait();
        try
        {
            var clone = DeepClone(optionsMonitor.CurrentValue);
            applyChanges(clone);

            var wrapper = new Dictionary<string, NjordOptions>
            {
                [NjordOptions.SectionName] = clone,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(overridePath)!);
            var json = JsonSerializer.Serialize(wrapper, SerializerOptions);
            var tempPath = overridePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, overridePath, overwrite: true);

            configurationRoot.Reload();

            return optionsMonitor.CurrentValue;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static NjordOptions DeepClone(NjordOptions source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<NjordOptions>(json)!;
    }
}
