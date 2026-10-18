using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Njord.Core.Configuration;

// Shared state of the runtime override file (data/appsettings.Override.json): the content of the last
// successful load, which the writer uses as the base when the file on disk turns corrupt, and the error
// reporting for load and write problems. The configuration is loaded before logging exists, so failures
// are buffered until the host attaches a logger.
public sealed class ConfigOverrideFile(string path)
{
    // The leniency of the JSON configuration loader, so every file njord loads can also be updated.
    public static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _gate = new();
    private readonly List<Action<ILogger>> _pending = [];
    private ILogger? _logger;
    private string? _lastGoodContent;
    private bool _migrationReported;

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public string? LastGoodContent
    {
        get
        {
            lock (_gate)
            {
                return _lastGoodContent;
            }
        }
    }

    public void RecordLoaded(string content)
    {
        lock (_gate)
        {
            _lastGoodContent = content;
        }
    }

    public void RecordLoadFailed(Exception error) =>
        Report(logger => logger.LogError(
            error,
            "Configuration override file {OverridePath} could not be loaded ({Reason}); keeping the last good "
            + "override values (none at startup: appsettings and environment variables only). Fix or delete the "
            + "file; the next configuration change moves it aside",
            Path,
            error.Message));

    public void RecordMovedAside(string corruptPath, Exception error) =>
        Report(logger => logger.LogError(
            error,
            "Configuration override file {OverridePath} is corrupt ({Reason}); moved it to {CorruptPath} and "
            + "wrote the last good override values plus the new change",
            Path,
            error.Message,
            corruptPath));

    // Reported once per process, however often the file is reloaded before the writer persists the new shape.
    public void RecordMigrated(IReadOnlyList<string> oldKeys)
    {
        lock (_gate)
        {
            if (_migrationReported)
            {
                return;
            }

            _migrationReported = true;
        }

        Report(logger => logger.LogWarning(
            "Configuration override file {OverridePath} uses keys removed in 0.5.0 ({OldKeys}); they are read from "
            + "their new sections and the file is rewritten with the new keys on the next configuration change",
            Path,
            string.Join(", ", oldKeys)));
    }

    public void AttachLogger(ILogger logger)
    {
        List<Action<ILogger>> pending;
        lock (_gate)
        {
            _logger = logger;
            pending = [.. _pending];
            _pending.Clear();
        }

        foreach (var write in pending)
        {
            write(logger);
        }
    }

    private void Report(Action<ILogger> write)
    {
        ILogger? logger;
        lock (_gate)
        {
            logger = _logger;
            if (logger is null)
            {
                _pending.Add(write);
                return;
            }
        }

        write(logger);
    }
}
