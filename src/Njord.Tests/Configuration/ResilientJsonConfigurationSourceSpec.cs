using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.Tests.Configuration;

public sealed class ResilientJsonConfigurationSourceSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-override-{Guid.NewGuid():N}");
    private readonly RecordingLogger _logger = new();

    public ResilientJsonConfigurationSourceSpec() => Directory.CreateDirectory(_tempDir);

    private string OverridePath => Path.Combine(_tempDir, "appsettings.Override.json");

    public static TheoryData<string> CorruptContents() =>
    [
        """{"Njord": {"ForecastDays": 7""",
        "",
        "   \n",
        "[1]",
        "\"just a string\"",
    ];

    [Theory]
    [MemberData(nameof(CorruptContents))]
    public void A_corrupt_override_file_at_startup_binds_the_remaining_layers(string content)
    {
        File.WriteAllText(OverridePath, content);

        var (root, file) = Build();

        file.AttachLogger(_logger);
        Assert.Multiple(
            () => Assert.Equal("4", root["Njord:ForecastDays"]),
            () => Assert.Null(file.LastGoodContent),
            () => Assert.Contains(OverridePath, Assert.Single(_logger.Errors).Message),
            () => Assert.Equal(content, File.ReadAllText(OverridePath)));
    }

    [Fact]
    public void An_unreadable_override_file_at_startup_binds_the_remaining_layers()
    {
        File.WriteAllText(OverridePath, """{"Njord": {"ForecastDays": 7}}""");
        using var lockStream = new FileStream(OverridePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var (root, file) = Build();

        file.AttachLogger(_logger);
        Assert.Equal("4", root["Njord:ForecastDays"]);
        var error = Assert.Single(_logger.Errors);
        Assert.Contains(OverridePath, error.Message);
        Assert.IsType<IOException>(error.Exception, exactMatch: false);
    }

    [Fact]
    public void A_valid_override_file_is_applied_and_remembered_as_last_good()
    {
        const string content = """{"Njord": {"ForecastDays": 7}}""";
        File.WriteAllText(OverridePath, content);

        var (root, file) = Build();

        Assert.Equal("7", root["Njord:ForecastDays"]);
        Assert.Equal(content, file.LastGoodContent);
    }

    [Fact]
    public void A_corrupt_reload_keeps_the_last_good_values_and_logs_an_error()
    {
        File.WriteAllText(OverridePath, """{"Njord": {"ForecastDays": 7, "Horizons": [6, 24]}}""");
        var (root, file) = Build();
        file.AttachLogger(_logger);

        File.WriteAllText(OverridePath, """{"Njord": {"ForecastDays": 9""");
        root.Reload();

        Assert.Multiple(
            () => Assert.Equal("7", root["Njord:ForecastDays"]),
            () => Assert.Equal("24", root["Njord:Horizons:1"]),
            () => Assert.Contains(OverridePath, Assert.Single(_logger.Errors).Message));
    }

    [Fact]
    public void A_repaired_file_is_applied_on_the_next_reload()
    {
        File.WriteAllText(OverridePath, """{"Njord": {"ForecastDays": 7}}""");
        var (root, _) = Build();
        File.WriteAllText(OverridePath, "{");
        root.Reload();

        File.WriteAllText(OverridePath, """{"Njord": {"ForecastDays": 9}}""");
        root.Reload();

        Assert.Equal("9", root["Njord:ForecastDays"]);
    }

    [Fact]
    public void A_deleted_override_file_falls_back_to_the_remaining_layers()
    {
        File.WriteAllText(OverridePath, """{"Njord": {"ForecastDays": 7}}""");
        var (root, _) = Build();

        File.Delete(OverridePath);
        root.Reload();

        Assert.Equal("4", root["Njord:ForecastDays"]);
    }

    // Real file watcher: no Akka wait, but a real-time bound, so the outer timeout is the hosted one.
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_watched_file_that_turns_corrupt_keeps_the_last_good_values_until_repaired()
    {
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(OverridePath, """{"Njord": {"ForecastDays": 7}}""", ct);
        var (root, file) = Build(reloadOnChange: true);
        file.AttachLogger(_logger);

        await File.WriteAllTextAsync(OverridePath, """{"Njord": {"ForecastDays": 9""", ct);
        await WaitUntil(() => _logger.Errors.Count > 0, "the corrupt reload to be logged", ct);
        Assert.Equal("7", root["Njord:ForecastDays"]);

        await File.WriteAllTextAsync(OverridePath, """{"Njord": {"ForecastDays": 9}}""", ct);
        await WaitUntil(() => root["Njord:ForecastDays"] == "9", "the repaired file to be applied", ct);
    }

    [Fact]
    public void Startup_validation_passes_over_a_corrupt_override_file()
    {
        File.WriteAllText(OverridePath, """{"Njord": """);
        using var provider = BuildHost();

        provider.GetRequiredService<IStartupValidator>().Validate();

        Assert.Equal(4, provider.GetRequiredService<IOptions<NjordOptions>>().Value.ForecastDays);
    }

    [Fact]
    public void A_readable_override_file_with_invalid_values_still_fails_startup_validation()
    {
        File.WriteAllText(OverridePath, """{"Njord": {"ForecastDays": 99}}""");
        using var provider = BuildHost();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("ForecastDays", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private (IConfigurationRoot Root, ConfigOverrideFile File) Build(bool reloadOnChange = false)
    {
        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Njord:ForecastDays"] = "4" });
        var file = builder.AddResilientJsonFile(OverridePath, reloadOnChange);
        return (builder.Build(), file);
    }

    private ServiceProvider BuildHost()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Samples", "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Samples", "appsettings.Example.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:PersistencePath"] = Path.Combine(_tempDir, "njord.db"),
            });
        builder.AddResilientJsonFile(OverridePath, reloadOnChange: false);
        return HostConfiguration.Services(builder.Build()).BuildServiceProvider();
    }

    private static async Task WaitUntil(Func<bool> condition, string what, CancellationToken ct)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(elapsed.Elapsed < TestTimeouts.RealTimeWaitMax, $"Timed out after {TestTimeouts.RealTimeWaitMax} waiting for {what}");
            await Task.Delay(50, ct);
        }
    }
}
