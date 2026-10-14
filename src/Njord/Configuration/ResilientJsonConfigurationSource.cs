using System.Text;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Primitives;
using Njord.Core.Configuration;

namespace Njord.Configuration;

public static class ResilientJsonConfigurationExtensions
{
    // Optional JSON file whose read or parse failures never throw: the provider keeps its last good data
    // (none at startup) and reports the failure to the returned ConfigOverrideFile.
    public static ConfigOverrideFile AddResilientJsonFile(this IConfigurationBuilder builder, string path, bool reloadOnChange)
    {
        var overrideFile = new ConfigOverrideFile(path);
        var source = new ResilientJsonConfigurationSource(overrideFile, reloadOnChange)
        {
            Path = path,
            Optional = true,
        };
        source.ResolveFileProvider();
        builder.Add(source);
        return overrideFile;
    }
}

public sealed class ResilientJsonConfigurationSource(ConfigOverrideFile overrideFile, bool watch) : JsonConfigurationSource
{
    public ConfigOverrideFile OverrideFile { get; } = overrideFile;

    // The base ReloadOnChange stays off: the provider runs its own watch so watcher reloads also go
    // through the failure handling (the stock watcher callback throws on a pool thread).
    public bool Watch { get; } = watch;

    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new ResilientJsonConfigurationProvider(this);
    }
}

public sealed class ResilientJsonConfigurationProvider : JsonConfigurationProvider
{
    private readonly ConfigOverrideFile _overrideFile;
    private readonly IDisposable? _watch;

    public ResilientJsonConfigurationProvider(ResilientJsonConfigurationSource source) : base(source)
    {
        _overrideFile = source.OverrideFile;
        if (source is { Watch: true, FileProvider: { } fileProvider, Path: { } path })
        {
            _watch = ChangeToken.OnChange(
                () => fileProvider.Watch(path),
                () =>
                {
                    Thread.Sleep(source.ReloadDelay);
                    Load();
                });
        }
    }

    // Opening the file happens outside the base class's own error handling (locked file, permissions).
    public override void Load()
    {
        try
        {
            base.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _overrideFile.RecordLoadFailed(ex);
        }
    }

    // Data is only replaced on a successful parse, so a failure keeps the last good values.
    public override void Load(Stream stream)
    {
        string content;
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            content = reader.ReadToEnd();
            base.Load(new MemoryStream(Encoding.UTF8.GetBytes(content)));
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            _overrideFile.RecordLoadFailed(ex is FormatException { InnerException: { } inner } ? inner : ex);
            return;
        }

        _overrideFile.RecordLoaded(content);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watch?.Dispose();
        }

        base.Dispose(disposing);
    }
}
