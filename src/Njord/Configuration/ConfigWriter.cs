using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Configuration;

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

            await PersistAsync([new PersistOperation(ConfigSections.PathOf<T>(), false, delta)], cancellationToken);
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

    public async Task<ConfigBatchResult> UpdateManyAsync(
        IReadOnlyList<SectionUpdate> updates, bool dryRun, CancellationToken cancellationToken)
    {
        if (updates.Select(u => u.OptionsType).Distinct().Count() != updates.Count)
        {
            throw new ArgumentException("A section can be updated only once per batch.", nameof(updates));
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var plans = updates.Select(u => u.Plan(this)).ToList();
            var outcomes = plans.Select(p => p.Outcome).ToList();
            if (dryRun || outcomes.Any(o => o.Failures.Count > 0))
            {
                return new ConfigBatchResult(false, outcomes);
            }

            var operations = plans.Where(p => p.Operation is not null).Select(p => p.Operation!).ToList();
            if (operations.Count > 0)
            {
                await PersistAsync(operations, cancellationToken);
                configurationRoot.Reload();
            }

            return new ConfigBatchResult(true, outcomes);
        }
        finally
        {
            _lock.Release();
        }
    }

    internal SectionPlan Plan<T>(Action<T> applyChanges, bool reset) where T : class, new()
    {
        var before = DeepClone(services.GetRequiredService<IOptionsMonitor<T>>().CurrentValue);
        var start = reset ? BindBase<T>() : before;
        var after = DeepClone(start);
        applyChanges(after);

        var failures = Validate(after);
        if (failures.Count > 0)
        {
            return new SectionPlan(new SectionOutcome(typeof(T), [], failures), null);
        }

        var changed = ComputeDelta(Serialize(before), Serialize(after));
        var delta = reset ? ComputeDelta(Serialize(start), Serialize(after)) : changed;
        var operation = reset || delta.Count > 0 ? new PersistOperation(ConfigSections.PathOf<T>(), reset, delta) : null;
        return new SectionPlan(new SectionOutcome(typeof(T), [.. changed.Select(p => p.Key)], []), operation);
    }

    // The section as the configuration would bind it without the override file: appsettings, environment, ...
    private T BindBase<T>() where T : class, new()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in configurationRoot.Providers.Where(p => !IsOverrideProvider(p)))
        {
            Collect(provider, null, data);
        }

        var section = new ConfigurationBuilder().AddInMemoryCollection(data).Build()
            .GetSection(ConfigSections.PathOf<T>());
        var value = new T();
        section.Bind(value);
        CollectionDefaults.Replace(value, section);
        return value;
    }

    private bool IsOverrideProvider(IConfigurationProvider provider) =>
        provider is FileConfigurationProvider { Source: { } source }
        && source.FileProvider?.GetFileInfo(source.Path ?? string.Empty).PhysicalPath is { } physical
        && string.Equals(
            Path.GetFullPath(physical),
            overrideFile.Path,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void Collect(IConfigurationProvider provider, string? parentPath, Dictionary<string, string?> data)
    {
        foreach (var key in provider.GetChildKeys([], parentPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = parentPath is null ? key : $"{parentPath}:{key}";
            if (provider.TryGet(path, out var value))
            {
                data[path] = value;
            }

            Collect(provider, path, data);
        }
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

    private async Task PersistAsync(IReadOnlyList<PersistOperation> operations, CancellationToken cancellationToken)
    {
        var root = await ReadExistingAsync(cancellationToken);

        foreach (var operation in operations)
        {
            var segments = operation.SectionPath.Split(':');
            if (operation.Remove)
            {
                RemovePath(root, segments);
            }

            if (operation.Delta.Count == 0)
            {
                continue;
            }

            var target = root;
            foreach (var segment in segments)
            {
                if (target[segment] is not JsonObject child)
                {
                    child = new JsonObject();
                    target[segment] = child;
                }

                target = child;
            }

            MergeInto(target, operation.Delta);
        }

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

    private static JsonObject ParseObject(string text)
    {
        var root = JsonNode.Parse(text, documentOptions: ConfigOverrideFile.ReadOptions) as JsonObject
            ?? throw new JsonException("The top-level JSON element is not an object.");
        LegacyConfigKeys.Migrate(root);
        return root;
    }

    private static void RemovePath(JsonObject root, string[] segments)
    {
        var chain = new List<JsonObject> { root };
        foreach (var segment in segments[..^1])
        {
            if (chain[^1][segment] is not JsonObject child)
            {
                return;
            }

            chain.Add(child);
        }

        chain[^1].Remove(segments[^1]);
        for (var i = chain.Count - 1; i > 0 && chain[i].Count == 0; i--)
        {
            chain[i - 1].Remove(segments[i - 1]);
        }
    }

    internal sealed record PersistOperation(string SectionPath, bool Remove, JsonObject Delta);

    internal sealed record SectionPlan(SectionOutcome Outcome, PersistOperation? Operation);

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
