using System.Text.Json;
using System.Text.Json.Nodes;
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
            var before = DeepClone(optionsMonitor.CurrentValue);
            var after = DeepClone(optionsMonitor.CurrentValue);
            applyChanges(after);

            var beforeJson = JsonSerializer.SerializeToNode(before, SerializerOptions)!.AsObject();
            var afterJson = JsonSerializer.SerializeToNode(after, SerializerOptions)!.AsObject();
            var delta = ComputeDelta(beforeJson, afterJson);

            var existing = ReadExistingOverrides();
            MergeInto(existing, delta);

            var wrapper = new JsonObject { [NjordOptions.SectionName] = existing.DeepClone() };

            Directory.CreateDirectory(Path.GetDirectoryName(overridePath)!);
            var json = wrapper.ToJsonString(SerializerOptions);
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

    private JsonObject ReadExistingOverrides()
    {
        if (!File.Exists(overridePath))
            return new JsonObject();

        var text = File.ReadAllText(overridePath);
        var root = JsonNode.Parse(text)?.AsObject();
        return root?[NjordOptions.SectionName]?.AsObject() ?? new JsonObject();
    }

    internal static JsonObject ComputeDelta(JsonObject before, JsonObject after)
    {
        var delta = new JsonObject();
        foreach (var (key, afterValue) in after)
        {
            var beforeValue = before[key];
            if (beforeValue is null && afterValue is null) continue;
            if (beforeValue is null || afterValue is null)
            {
                delta[key] = afterValue?.DeepClone();
                continue;
            }

            if (beforeValue is JsonObject beforeObj && afterValue is JsonObject afterObj)
            {
                var nested = ComputeDelta(beforeObj, afterObj);
                if (nested.Count > 0) delta[key] = nested;
            }
            else if (!JsonNode.DeepEquals(beforeValue, afterValue))
            {
                delta[key] = afterValue.DeepClone();
            }
        }

        foreach (var (key, beforeValue) in before)
        {
            if (beforeValue is not null && !after.ContainsKey(key))
            {
                delta[key] = null;
            }
        }

        return delta;
    }

    internal static void MergeInto(JsonObject target, JsonObject source)
    {
        foreach (var (key, sourceValue) in source)
        {
            if (sourceValue is JsonObject sourceObj && target[key] is JsonObject targetObj)
            {
                MergeInto(targetObj, sourceObj);
            }
            else
            {
                target[key] = sourceValue?.DeepClone();
            }
        }
    }
}
