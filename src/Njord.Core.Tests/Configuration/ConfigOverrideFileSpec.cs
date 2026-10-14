using System.Text.Json;
using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration;

public sealed class ConfigOverrideFileSpec
{
    private static readonly string FilePath = Path.Combine("data", "appsettings.Override.json");

    [Fact]
    public void A_failure_before_a_logger_is_attached_is_logged_as_error_on_attach()
    {
        var file = new ConfigOverrideFile(FilePath);
        var logger = new RecordingLogger();

        file.RecordLoadFailed(new JsonException("'2' is an invalid end of a number."));
        Assert.Empty(logger.Entries);
        file.AttachLogger(logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Multiple(
            () => Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entry.Level),
            () => Assert.Contains(Path.GetFullPath(FilePath), entry.Message),
            () => Assert.Contains("'2' is an invalid end of a number.", entry.Message),
            () => Assert.IsType<JsonException>(entry.Exception));
    }

    [Fact]
    public void A_failure_after_the_logger_is_attached_is_logged_immediately()
    {
        var file = new ConfigOverrideFile(FilePath);
        var logger = new RecordingLogger();
        file.AttachLogger(logger);

        file.RecordLoadFailed(new IOException("locked"));

        var entry = Assert.Single(logger.Errors);
        Assert.Contains("locked", entry.Message);
    }

    [Fact]
    public void The_last_successfully_loaded_content_is_kept_across_failures()
    {
        var file = new ConfigOverrideFile(FilePath);
        Assert.Null(file.LastGoodContent);

        file.RecordLoaded("""{"Njord":{"ForecastDays":7}}""");
        file.RecordLoadFailed(new JsonException("truncated"));

        Assert.Equal("""{"Njord":{"ForecastDays":7}}""", file.LastGoodContent);
    }

    [Fact]
    public void Moving_a_corrupt_file_aside_is_logged_as_error_naming_both_files()
    {
        var file = new ConfigOverrideFile(FilePath);
        var logger = new RecordingLogger();
        file.AttachLogger(logger);
        var corruptPath = Path.GetFullPath(FilePath) + ".corrupt-20261014T120000000Z";

        file.RecordMovedAside(corruptPath, new JsonException("truncated"));

        var entry = Assert.Single(logger.Errors);
        Assert.Multiple(
            () => Assert.Contains(Path.GetFullPath(FilePath), entry.Message),
            () => Assert.Contains(corruptPath, entry.Message),
            () => Assert.Contains("truncated", entry.Message));
    }
}
