using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Polling;

namespace Njord.Core.Tests.Configuration;

public sealed class ConfigWriterSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-writer-{Guid.NewGuid():N}");

    private string OverridePath => Path.Combine(_tempDir, "appsettings.Override.json");

    [Fact(Timeout = 5000)]
    public async Task Update_persists_the_change_under_the_section_path()
    {
        var (writer, _) = CreateWriter();

        await writer.UpdateAsync<AlertOptions>(opt => opt.Enabled = false, TestContext.Current.CancellationToken);

        var root = ReadOverride();
        Assert.False(root["Njord"]!["Enrichment"]!["Alerts"]!["Enabled"]!.GetValue<bool>());
    }

    [Fact(Timeout = 5000)]
    public async Task Update_reloads_so_the_monitor_sees_the_new_value()
    {
        var (writer, provider) = CreateWriter(alertsEnabled: true);
        var monitor = provider.GetRequiredService<IOptionsMonitor<AlertOptions>>();
        Assert.True(monitor.CurrentValue.Enabled);

        var result = await writer.UpdateAsync<AlertOptions>(opt => opt.Enabled = false, TestContext.Current.CancellationToken);

        Assert.True(result.Applied);
        Assert.False(result.Value.Enabled);
        Assert.False(monitor.CurrentValue.Enabled);
    }

    [Fact]
    public void Override_file_is_not_created_at_startup()
    {
        CreateWriter();

        Assert.False(File.Exists(OverridePath));
    }

    [Fact(Timeout = 5000)]
    public async Task Override_file_is_created_on_first_write()
    {
        var (writer, _) = CreateWriter();

        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 7, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(OverridePath));
        Assert.False(File.Exists(OverridePath + ".tmp"));
    }

    [Fact(Timeout = 5000)]
    public async Task Only_changed_properties_are_written()
    {
        var (writer, _) = CreateWriter();

        var result = await writer.UpdateAsync<NjordOptions>(opt => opt.Horizons = [6, 24], TestContext.Current.CancellationToken);

        var njord = ReadOverride()["Njord"]!.AsObject();
        Assert.True(njord.ContainsKey("Horizons"));
        Assert.False(njord.ContainsKey("ForecastDays"));
        Assert.False(njord.ContainsKey("Locations"));
        Assert.Equal(["Horizons"], result.ChangedProperties);
    }

    [Fact(Timeout = 5000)]
    public async Task Arrays_are_replaced_not_merged()
    {
        var (writer, provider) = CreateWriter();

        await writer.UpdateAsync<NjordOptions>(opt => opt.Horizons = [6, 24], TestContext.Current.CancellationToken);

        var horizons = ReadOverride()["Njord"]!["Horizons"]!.AsArray();
        Assert.Equal(2, horizons.Count);
        Assert.Equal(6, horizons[0]!.GetValue<int>());
        Assert.Equal(24, horizons[1]!.GetValue<int>());
        Assert.Equal([6, 24], provider.GetRequiredService<IOptionsMonitor<NjordOptions>>().CurrentValue.Horizons);
    }

    [Fact(Timeout = 5000)]
    public async Task Writes_to_different_sections_accumulate_in_one_file()
    {
        var (writer, _) = CreateWriter();
        var ct = TestContext.Current.CancellationToken;

        await writer.UpdateAsync<NjordOptions>(opt => opt.Horizons = [6, 24], ct);
        await writer.UpdateAsync<AlertOptions>(opt => opt.Enabled = false, ct);
        await writer.UpdateAsync<PollingOptions>(opt => opt.PollInterval = TimeSpan.FromMinutes(30), ct);

        var njord = ReadOverride()["Njord"]!;
        Assert.Equal(2, njord["Horizons"]!.AsArray().Count);
        Assert.False(njord["Enrichment"]!["Alerts"]!["Enabled"]!.GetValue<bool>());
        Assert.Equal("00:30:00", njord["PollInterval"]!.GetValue<string>());
    }

    [Fact(Timeout = 5000)]
    public async Task An_existing_override_file_keeps_its_unrelated_keys()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(
            OverridePath,
            """{"Njord": {"ForecastDays": 5, "Enrichment": {"Alerts": {"HeatThresholds": [31, 36, 41]}}}}""",
            TestContext.Current.CancellationToken);
        var (writer, provider) = CreateWriter();

        await writer.UpdateAsync<AlertOptions>(opt => opt.Enabled = false, TestContext.Current.CancellationToken);

        var njord = ReadOverride()["Njord"]!;
        Assert.Equal(5, njord["ForecastDays"]!.GetValue<int>());
        Assert.Equal(3, njord["Enrichment"]!["Alerts"]!["HeatThresholds"]!.AsArray().Count);
        Assert.Equal(5, provider.GetRequiredService<IOptionsMonitor<NjordOptions>>().CurrentValue.ForecastDays);
    }

    [Fact(Timeout = 5000)]
    public async Task Concurrent_updates_are_all_persisted()
    {
        var (writer, provider) = CreateWriter();
        var ct = TestContext.Current.CancellationToken;

        await Task.WhenAll(
            Task.Run(() => writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 6, ct), ct),
            Task.Run(() => writer.UpdateAsync<AlertOptions>(opt => opt.CapeThreshold = 1500, ct), ct),
            Task.Run(() => writer.UpdateAsync<PollingOptions>(opt => opt.PollInterval = TimeSpan.FromMinutes(90), ct), ct));

        Assert.Equal(6, provider.GetRequiredService<IOptionsMonitor<NjordOptions>>().CurrentValue.ForecastDays);
        Assert.Equal(1500, provider.GetRequiredService<IOptionsMonitor<AlertOptions>>().CurrentValue.CapeThreshold);
        Assert.Equal(TimeSpan.FromMinutes(90), provider.GetRequiredService<IOptionsMonitor<PollingOptions>>().CurrentValue.PollInterval);
    }

    [Fact(Timeout = 5000)]
    public async Task An_invalid_update_is_not_persisted()
    {
        var (writer, provider) = CreateWriter();

        var result = await writer.UpdateAsync<ConsensusOptions>(
            opt =>
            {
                opt.Method = Compute.Configuration.ConsensusMethod.TrimmedMean;
                opt.TrimPercent = 0.7;
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.Applied);
        Assert.Contains(result.Failures, f => f.Contains("TrimPercent"));
        Assert.False(File.Exists(OverridePath));
        Assert.Equal(0.1, provider.GetRequiredService<IOptionsMonitor<ConsensusOptions>>().CurrentValue.TrimPercent);
    }

    [Fact(Timeout = 5000)]
    public async Task An_invalid_update_leaves_an_existing_file_unchanged()
    {
        var (writer, _) = CreateWriter();
        var ct = TestContext.Current.CancellationToken;
        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 6, ct);
        var before = await File.ReadAllTextAsync(OverridePath, ct);

        var result = await writer.UpdateAsync<AlertOptions>(opt => opt.FrostThresholds = [0, 5, -15], ct);

        Assert.False(result.Applied);
        Assert.Equal(before, await File.ReadAllTextAsync(OverridePath, ct));
    }

    [Fact]
    public void Check_reports_failures_without_writing()
    {
        var (writer, _) = CreateWriter();

        var failures = writer.Check<AlertOptions>(opt => opt.HeatThresholds = [40, 35, 30]);

        Assert.Contains(failures, f => f.Contains("HeatThresholds"));
        Assert.False(File.Exists(OverridePath));
    }

    [Fact(Timeout = 5000)]
    public async Task A_restart_required_change_is_reported()
    {
        var (writer, _) = CreateWriter();
        var ct = TestContext.Current.CancellationToken;

        var toggle = await writer.UpdateAsync<AlertOptions>(opt => opt.Enabled = false, ct);
        var threshold = await writer.UpdateAsync<AlertOptions>(opt => opt.CapeThreshold = 1200, ct);

        Assert.True(toggle.RequiresRestart);
        Assert.False(threshold.RequiresRestart);
    }

    [Fact(Timeout = 5000)]
    public async Task A_successful_write_signals_the_change_feed()
    {
        var feed = new ConfigChangeFeed();
        var signals = 0;
        using var _ = feed.Subscribe(() => Interlocked.Increment(ref signals));
        var (writer, _) = CreateWriter(feed: feed);

        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 7, TestContext.Current.CancellationToken);

        Assert.Equal(1, signals);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private JsonNode ReadOverride() => JsonNode.Parse(File.ReadAllText(OverridePath))!;

    private (ConfigWriter Writer, ServiceProvider Provider) CreateWriter(bool alertsEnabled = true, ConfigChangeFeed? feed = null)
    {
        var configRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:Locations:0:Name"] = "lucerne",
                ["Njord:Locations:0:Latitude"] = "47.05",
                ["Njord:Locations:0:Longitude"] = "8.31",
                ["Njord:Models:0"] = "icon_d2",
                ["Njord:Enrichment:Alerts:Enabled"] = alertsEnabled.ToString(),
            })
            .AddJsonFile(OverridePath, optional: true, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configRoot);
        services.AddOptions<NjordOptions>().BindConfiguration(NjordOptions.SectionName).ReplaceCollectionDefaults();
        services.AddSingleton<IValidateOptions<NjordOptions>, NjordOptionsValidator>();
        services.AddOptions<PollingOptions>().BindConfiguration(PollingOptions.SectionName);
        services.AddSingleton<IValidateOptions<PollingOptions>, PollingOptionsValidator>();
        services.AddOptions<AlertOptions>().BindConfiguration(AlertOptions.SectionName).ReplaceCollectionDefaults();
        services.AddSingleton<IValidateOptions<AlertOptions>, AlertOptionsValidator>();
        services.AddOptions<ConsensusOptions>().BindConfiguration(ConsensusOptions.SectionName);
        services.AddSingleton<IValidateOptions<ConsensusOptions>, ConsensusOptionsValidator>();
        var provider = services.BuildServiceProvider();

        return (new ConfigWriter(provider, configRoot, feed ?? new ConfigChangeFeed(), OverridePath), provider);
    }
}
