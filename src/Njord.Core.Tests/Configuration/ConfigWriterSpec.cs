using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Polling;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration;

public sealed class ConfigWriterSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-writer-{Guid.NewGuid():N}");

    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, 123, TimeSpan.Zero);

    private string OverridePath => Path.Combine(_tempDir, "appsettings.Override.json");

    private readonly ConfigOverrideFile _overrideFile;

    public ConfigWriterSpec() => _overrideFile = new ConfigOverrideFile(OverridePath);

    private string CorruptPath => OverridePath + ".corrupt-20261014T120000123Z";

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
                opt.Method = Compute.Settings.ConsensusMethod.TrimmedMean;
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
    public async Task A_corrupt_override_file_is_moved_aside_with_its_original_bytes_before_the_write()
    {
        var ct = TestContext.Current.CancellationToken;
        var (writer, provider) = CreateWriter();
        await File.WriteAllTextAsync(OverridePath, """{"Njord": {"Horizons": [6, 2""", ct);

        var result = await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 7, ct);

        Assert.True(result.Applied);
        Assert.Equal("""{"Njord": {"Horizons": [6, 2""", await File.ReadAllTextAsync(CorruptPath, ct));
        var njord = ReadOverride()["Njord"]!.AsObject();
        Assert.Multiple(
            () => Assert.Equal(["ForecastDays"], njord.Select(p => p.Key)),
            () => Assert.Equal(7, njord["ForecastDays"]!.GetValue<int>()),
            () => Assert.Equal(7, provider.GetRequiredService<IOptionsMonitor<NjordOptions>>().CurrentValue.ForecastDays));
    }

    [Fact(Timeout = 5000)]
    public async Task Moving_a_corrupt_override_file_aside_is_logged_as_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new RecordingLogger();
        var (writer, _) = CreateWriter(logger: logger);
        await File.WriteAllTextAsync(OverridePath, "", ct);

        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 7, ct);

        var entry = Assert.Single(logger.Errors);
        Assert.Multiple(
            () => Assert.Contains(Path.GetFullPath(OverridePath), entry.Message),
            () => Assert.Contains(Path.GetFullPath(CorruptPath), entry.Message));
    }

    [Fact(Timeout = 5000)]
    public async Task A_rewrite_over_a_corrupt_file_keeps_the_last_good_overrides()
    {
        var ct = TestContext.Current.CancellationToken;
        const string lastGood = """{"Njord": {"Horizons": [6, 24]}}""";
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(OverridePath, lastGood, ct);
        var (writer, provider) = CreateWriter();
        _overrideFile.RecordLoaded(lastGood);
        await File.WriteAllTextAsync(OverridePath, "[1]", ct);

        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 7, ct);

        var njord = ReadOverride()["Njord"]!;
        var options = provider.GetRequiredService<IOptionsMonitor<NjordOptions>>().CurrentValue;
        Assert.Multiple(
            () => Assert.Equal([6, 24], njord["Horizons"]!.AsArray().Select(h => h!.GetValue<int>())),
            () => Assert.Equal(7, njord["ForecastDays"]!.GetValue<int>()),
            () => Assert.Equal([6, 24], options.Horizons),
            () => Assert.Equal(7, options.ForecastDays),
            () => Assert.Equal("[1]", File.ReadAllText(CorruptPath)));
    }

    [Fact(Timeout = 5000)]
    public async Task A_hand_edited_override_with_comments_and_trailing_commas_is_updated()
    {
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(OverridePath, """
            {
              // set by hand
              "Njord": { "ForecastDays": 7, },
            }
            """, ct);
        var (writer, _) = CreateWriter();

        var result = await writer.UpdateAsync<PollingOptions>(opt => opt.PollInterval = TimeSpan.FromMinutes(30), ct);

        var njord = ReadOverride()["Njord"]!;
        Assert.Multiple(
            () => Assert.True(result.Applied),
            () => Assert.Equal(7, njord["ForecastDays"]!.GetValue<int>()),
            () => Assert.Equal("00:30:00", njord["PollInterval"]!.GetValue<string>()),
            () => Assert.False(File.Exists(CorruptPath)));
    }

    [Fact(Timeout = 5000)]
    public async Task A_successful_write_leaves_only_the_override_file_in_the_directory()
    {
        var ct = TestContext.Current.CancellationToken;
        var (writer, _) = CreateWriter();

        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 7, ct);
        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 8, ct);

        Assert.Equal([Path.GetFullPath(OverridePath)], Directory.GetFiles(_tempDir).Select(Path.GetFullPath));
    }

    [Fact(Timeout = 5000)]
    public async Task Update_many_persists_all_sections_with_one_reload()
    {
        var (writer, provider) = CreateWriter();
        var root = provider.GetRequiredService<IConfiguration>();
        var reloads = 0;
        using var subscription = ChangeToken.OnChange(root.GetReloadToken, () => reloads++);
        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 6, TestContext.Current.CancellationToken);
        var reloadsPerWrite = reloads;
        reloads = 0;

        var result = await writer.UpdateManyAsync(
            [
                SectionUpdate.For<AlertOptions>(opt => opt.CapeThreshold = 1800),
                SectionUpdate.For<ConsensusOptions>(opt => opt.TrimPercent = 0.2),
            ],
            dryRun: false,
            TestContext.Current.CancellationToken);

        var enrichment = ReadOverride()["Njord"]!["Enrichment"]!;
        Assert.Multiple(
            () => Assert.True(result.Applied),
            () => Assert.Equal(reloadsPerWrite, reloads),
            () => Assert.Equal(1800, enrichment["Alerts"]!["CapeThreshold"]!.GetValue<double>()),
            () => Assert.Equal(0.2, enrichment["Consensus"]!["TrimPercent"]!.GetValue<double>()),
            () => Assert.Equal(["CapeThreshold"], result.Sections[0].ChangedProperties),
            () => Assert.Equal(1800, provider.GetRequiredService<IOptionsMonitor<AlertOptions>>().CurrentValue.CapeThreshold),
            () => Assert.Equal(0.2, provider.GetRequiredService<IOptionsMonitor<ConsensusOptions>>().CurrentValue.TrimPercent));
    }

    [Fact(Timeout = 5000)]
    public async Task Update_many_persists_nothing_when_one_section_is_invalid()
    {
        var (writer, provider) = CreateWriter();
        var root = provider.GetRequiredService<IConfiguration>();
        var reloads = 0;
        using var subscription = ChangeToken.OnChange(root.GetReloadToken, () => reloads++);

        var result = await writer.UpdateManyAsync(
            [
                SectionUpdate.For<ConsensusOptions>(opt => opt.TrimPercent = 0.2),
                SectionUpdate.For<AlertOptions>(opt => opt.FrostThresholds = [0, 5, -15]),
            ],
            dryRun: false,
            TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.False(result.Applied),
            () => Assert.False(result.Valid),
            () => Assert.Empty(result.Sections[0].Failures),
            () => Assert.Contains(result.Sections[1].Failures, f => f.Contains("FrostThresholds")),
            () => Assert.Equal(typeof(AlertOptions), result.Sections[1].OptionsType),
            () => Assert.False(File.Exists(OverridePath)),
            () => Assert.Equal(0, reloads),
            () => Assert.Equal(0.1, provider.GetRequiredService<IOptionsMonitor<ConsensusOptions>>().CurrentValue.TrimPercent));
    }

    [Fact(Timeout = 5000)]
    public async Task Update_many_dry_run_validates_without_persisting()
    {
        var (writer, provider) = CreateWriter();

        var valid = await writer.UpdateManyAsync(
            [SectionUpdate.For<ConsensusOptions>(opt => opt.TrimPercent = 0.2)], dryRun: true, TestContext.Current.CancellationToken);
        var invalid = await writer.UpdateManyAsync(
            [SectionUpdate.For<ConsensusOptions>(opt =>
            {
                opt.Method = Compute.Settings.ConsensusMethod.TrimmedMean;
                opt.TrimPercent = 0.7;
            })],
            dryRun: true,
            TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.True(valid.Valid),
            () => Assert.False(valid.Applied),
            () => Assert.Equal(["TrimPercent"], valid.Sections[0].ChangedProperties),
            () => Assert.False(invalid.Valid),
            () => Assert.Contains(invalid.Sections[0].Failures, f => f.Contains("TrimPercent")),
            () => Assert.False(File.Exists(OverridePath)),
            () => Assert.Equal(0.1, provider.GetRequiredService<IOptionsMonitor<ConsensusOptions>>().CurrentValue.TrimPercent));
    }

    [Fact(Timeout = 5000)]
    public async Task Reset_removes_the_section_override_so_the_base_configuration_applies_again()
    {
        var (writer, provider) = CreateWriter(alertsEnabled: true);
        var ct = TestContext.Current.CancellationToken;
        await writer.UpdateAsync<AlertOptions>(opt =>
        {
            opt.Enabled = false;
            opt.HeatThresholds = [31, 36, 41];
        }, ct);
        await writer.UpdateAsync<NjordOptions>(opt => opt.ForecastDays = 6, ct);
        var defaultHeat = new AlertOptions().HeatThresholds;
        var monitor = provider.GetRequiredService<IOptionsMonitor<AlertOptions>>();
        Assert.False(monitor.CurrentValue.Enabled);

        var result = await writer.UpdateManyAsync(
            [SectionUpdate.For<AlertOptions>(_ => { }, reset: true)], dryRun: false, ct);

        var njord = ReadOverride()["Njord"]!.AsObject();
        Assert.Multiple(
            () => Assert.True(result.Applied),
            () => Assert.Equal(["Enabled", "HeatThresholds"], result.Sections[0].ChangedProperties.Order()),
            () => Assert.Equal(["Enabled"], result.Sections[0].RestartRequiredProperties),
            () => Assert.False(njord.ContainsKey("Enrichment")),
            () => Assert.Equal(6, njord["ForecastDays"]!.GetValue<int>()),
            () => Assert.True(monitor.CurrentValue.Enabled),
            () => Assert.Equal(defaultHeat, monitor.CurrentValue.HeatThresholds));
    }

    [Fact(Timeout = 5000)]
    public async Task Reset_keeps_sibling_sections_of_the_override()
    {
        var (writer, provider) = CreateWriter();
        var ct = TestContext.Current.CancellationToken;
        await writer.UpdateAsync<AlertOptions>(opt => opt.CapeThreshold = 1800, ct);
        await writer.UpdateAsync<ConsensusOptions>(opt => opt.TrimPercent = 0.2, ct);

        await writer.UpdateManyAsync([SectionUpdate.For<AlertOptions>(_ => { }, reset: true)], dryRun: false, ct);

        var enrichment = ReadOverride()["Njord"]!["Enrichment"]!.AsObject();
        Assert.Multiple(
            () => Assert.False(enrichment.ContainsKey("Alerts")),
            () => Assert.Equal(0.2, enrichment["Consensus"]!["TrimPercent"]!.GetValue<double>()),
            () => Assert.Equal(0.2, provider.GetRequiredService<IOptionsMonitor<ConsensusOptions>>().CurrentValue.TrimPercent));
    }

    [Fact(Timeout = 5000)]
    public async Task Reset_then_update_applies_the_change_on_top_of_the_base_configuration()
    {
        var (writer, provider) = CreateWriter(alertsEnabled: true);
        var ct = TestContext.Current.CancellationToken;
        await writer.UpdateAsync<AlertOptions>(opt => opt.CapeThreshold = 1800, ct);

        var result = await writer.UpdateManyAsync(
            [SectionUpdate.For<AlertOptions>(opt => opt.Enabled = false, reset: true)], dryRun: false, ct);

        var alerts = ReadOverride()["Njord"]!["Enrichment"]!["Alerts"]!.AsObject();
        var current = provider.GetRequiredService<IOptionsMonitor<AlertOptions>>().CurrentValue;
        Assert.Multiple(
            () => Assert.True(result.Applied),
            () => Assert.Equal(["Enabled"], alerts.Select(p => p.Key)),
            () => Assert.False(current.Enabled),
            () => Assert.Equal(new AlertOptions().CapeThreshold, current.CapeThreshold));
    }

    [Fact(Timeout = 5000)]
    public async Task Reset_without_an_override_file_succeeds_and_changes_nothing()
    {
        var (writer, provider) = CreateWriter(alertsEnabled: false);

        var result = await writer.UpdateManyAsync(
            [SectionUpdate.For<AlertOptions>(_ => { }, reset: true)], dryRun: false, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.True(result.Applied),
            () => Assert.Empty(result.Sections[0].ChangedProperties),
            () => Assert.False(provider.GetRequiredService<IOptionsMonitor<AlertOptions>>().CurrentValue.Enabled));
    }

    [Fact(Timeout = 5000)]
    public async Task Update_many_rejects_the_same_section_twice()
    {
        var (writer, _) = CreateWriter();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => writer.UpdateManyAsync(
            [SectionUpdate.For<AlertOptions>(_ => { }), SectionUpdate.For<AlertOptions>(_ => { })],
            dryRun: false,
            TestContext.Current.CancellationToken));

        Assert.Equal("updates", ex.ParamName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private JsonNode ReadOverride() => JsonNode.Parse(File.ReadAllText(OverridePath))!;

    private (ConfigWriter Writer, ServiceProvider Provider) CreateWriter(
        bool alertsEnabled = true, RecordingLogger? logger = null)
    {
        Directory.CreateDirectory(_tempDir);
        _overrideFile.AttachLogger(logger ?? new RecordingLogger());
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

        return (new ConfigWriter(provider, configRoot, _overrideFile, TestTimeouts.AtEpoch(Now)), provider);
    }
}
