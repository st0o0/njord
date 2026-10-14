using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class ConfigWriter(
    IServiceProvider services,
    IConfigurationRoot configurationRoot,
    ConfigOverrideFile overrideFile,
    TimeProvider timeProvider) : IConfigWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // The leniency of the JSON configuration loader, so every file njord loads can also be updated.
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<ConfigUpdateResult<T>> UpdateAsync<T>(Action<T> applyChanges, CancellationToken cancellationToken)
        where T : class, new()
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var monitor = services.GetRequiredService<IOptionsMonitor<T>>();
            var before = DeepClone(monitor.CurrentValue);
            var after = DeepClone(monitor.CurrentValue);
            applyChanges(after);

            var failures = Validate(after);
            if (failures.Count > 0)
            {
                return new ConfigUpdateResult<T>(false, before, [], failures);
            }

            var delta = ComputeDelta(Serialize(before), Serialize(after));
            if (delta.Count == 0)
            {
                return new ConfigUpdateResult<T>(true, after, [], []);
            }

            await PersistAsync(ConfigSections.PathOf<T>(), delta, cancellationToken);
            configurationRoot.Reload();

            return new ConfigUpdateResult<T>(true, monitor.CurrentValue, [.. delta.Select(p => p.Key)], []);
        }
        finally
        {
            _lock.Release();
        }
    }

    public IReadOnlyList<string> Check<T>(Action<T> applyChanges) where T : class, new()
    {
        var candidate = DeepClone(services.GetRequiredService<IOptionsMonitor<T>>().CurrentValue);
        applyChanges(candidate);
        return Validate(candidate);
    }

    public static T DeepClone<T>(T source) where T : class =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, SerializerOptions), SerializerOptions)!;

    private List<string> Validate<T>(T candidate) where T : class
    {
        var failures = new List<string>();
        foreach (var validator in services.GetServices<IValidateOptions<T>>())
        {
            var result = validator.Validate(Options.DefaultName, candidate);
            if (result.Failed)
            {
                failures.AddRange(result.Failures ?? [result.FailureMessage]);
            }
        }

        return failures;
    }

    private static JsonObject Serialize<T>(T value) =>
        JsonSerializer.SerializeToNode(value, SerializerOptions)!.AsObject();

    private async Task PersistAsync(string sectionPath, JsonObject delta, CancellationToken cancellationToken)
    {
        var root = await ReadExistingAsync(cancellationToken);

        var target = root;
        foreach (var segment in sectionPath.Split(':'))
        {
            if (target[segment] is not JsonObject child)
            {
                child = new JsonObject();
                target[segment] = child;
            }

            target = child;
        }

        MergeInto(target, delta);

        var path = overrideFile.Path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, root, SerializerOptions, cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    // A corrupt file is never merged into or overwritten: it is moved aside and the new file starts from the
    // last good content the configuration loaded, so the persisted overrides match the running configuration.
    private async Task<JsonObject> ReadExistingAsync(CancellationToken cancellationToken)
    {
        var path = overrideFile.Path;
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            return ParseObject(text);
        }
        catch (JsonException ex)
        {
            var stamp = timeProvider.GetUtcNow().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            var corruptPath = $"{path}.corrupt-{stamp}";
            File.Move(path, corruptPath, overwrite: false);
            overrideFile.RecordMovedAside(corruptPath, ex);
            return overrideFile.LastGoodContent is { } lastGood ? ParseObject(lastGood) : new JsonObject();
        }
    }

    private static JsonObject ParseObject(string text) =>
        JsonNode.Parse(text, documentOptions: ReadOptions) as JsonObject
        ?? throw new JsonException("The top-level JSON element is not an object.");

    internal static JsonObject ComputeDelta(JsonObject before, JsonObject after)
    {
        var delta = new JsonObject();
        foreach (var (key, afterValue) in after)
        {
            var beforeValue = before[key];
            if (beforeValue is null && afterValue is null)
            {
                continue;
            }

            if (beforeValue is null || afterValue is null)
            {
                delta[key] = afterValue?.DeepClone();
                continue;
            }

            if (beforeValue is JsonObject beforeObj && afterValue is JsonObject afterObj)
            {
                var nested = ComputeDelta(beforeObj, afterObj);
                if (nested.Count > 0)
                {
                    delta[key] = nested;
                }
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
