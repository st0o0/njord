using System.Globalization;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;
using Njord.Configuration;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;
using Njord.Core.Health;
using Njord.Messages.Config;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Tests.Configuration;

public sealed class ConfigManagerSpec : NjordActorSpec
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(9);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-config-manager-{Guid.NewGuid():N}");
    private readonly List<ConfigHarness> _harnesses = [];
    private readonly ConfigHarness _harness;
    private readonly NjordHealthMonitor _health = new();

    protected override bool UseTestPersistence => false;

    public ConfigManagerSpec()
    {
        _harness = CreateHarness(new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = "lucerne",
            ["Njord:Locations:0:Latitude"] = "47.05",
            ["Njord:Locations:0:Longitude"] = "8.31",
            ["Njord:Models:0"] = "icon_d2",
        });
    }

    private ConfigHarness CreateHarness(IDictionary<string, string?> baseline)
    {
        var harness = new ConfigHarness(baseline, Path.Combine(_tempDir, $"override-{_harnesses.Count}.json"));
        _harnesses.Add(harness);
        return harness;
    }

    private async Task<(IActorRef Manager, TestProbe Pipeline, TestProbe Scheduler)> StartManagerAsync(ConfigHarness? harness = null)
    {
        var h = harness ?? _harness;
        var pipeline = CreateTestProbe();
        var scheduler = CreateTestProbe();
        ActorRegistry.Register<IForecastPipeline>(pipeline.Ref, overwrite: true);
        ActorRegistry.Register<IPollScheduler>(scheduler.Ref, overwrite: true);

        var startup = AnalysisSettingsFactory.Create(
            h.Monitor<NjordOptions>().CurrentValue,
            h.Monitor<ConsensusOptions>().CurrentValue,
            h.Monitor<AlertOptions>().CurrentValue,
            h.Monitor<IndexOptions>().CurrentValue,
            h.Monitor<TrendOptions>().CurrentValue,
            h.Monitor<DerivedOptions>().CurrentValue);

        var manager = Sys.ActorOf(Props.Create(() => new ConfigManager(
            Options.Create(h.Monitor<NjordOptions>().CurrentValue),
            startup,
            Options.Create(h.Monitor<PollingOptions>().CurrentValue),
            h.Monitor<NjordOptions>(),
            h.Monitor<PollingOptions>(),
            h.Monitor<ConsensusOptions>(),
            h.Monitor<AlertOptions>(),
            h.Monitor<IndexOptions>(),
            h.Monitor<TrendOptions>(),
            h.Monitor<DerivedOptions>(),
            h.Writer,
            _health)), $"config-manager-{Guid.NewGuid():N}");

        // PreStart (where the change subscriptions are made) has run once the actor answers.
        await manager.Ask<ActorIdentity>(new Identify(1), AskTimeout, TestContext.Current.CancellationToken);
        return (manager, pipeline, scheduler);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_alert_threshold_reload_reaches_the_pipeline()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, pipeline, scheduler) = await StartManagerAsync();

        await _harness.Writer.UpdateAsync<AlertOptions>(opt => opt.FrostThresholds = [1, -4, -12], ct);

        var changed = await pipeline.ExpectMsgAsync<AnalysisSettingsChanged>(cancellationToken: ct);
        Assert.Equal(new SeverityThresholds(1, -4, -12, ScanDirection.Min), changed.Settings.Alerts.Frost);
        await scheduler.ExpectNoMsgAsync(Quiet, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_analysis_settings_query_answers_the_current_settings_after_a_reload()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, pipeline, _) = await StartManagerAsync();
        await _harness.Writer.UpdateAsync<AlertOptions>(opt => opt.FrostThresholds = [2, -3, -9], ct);
        await pipeline.ExpectMsgAsync<AnalysisSettingsChanged>(cancellationToken: ct);

        var response = await manager.Ask<AnalysisSettingsResponse>(new QueryAnalysisSettings(), AskTimeout, ct);

        var result = Assert.IsType<AnalysisSettingsResult>(response);
        Assert.Equal(new SeverityThresholds(2, -3, -9, ScanDirection.Min), result.Settings.Alerts.Frost);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_poll_interval_reload_reaches_the_scheduler()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, pipeline, scheduler) = await StartManagerAsync();

        await _harness.Writer.UpdateAsync<PollingOptions>(opt => opt.PollInterval = TimeSpan.FromMinutes(30), ct);

        var changed = await scheduler.ExpectMsgAsync<PollingChanged>(cancellationToken: ct);
        Assert.Equal(TimeSpan.FromMinutes(30), changed.PollInterval);
        await pipeline.ExpectNoMsgAsync(Quiet, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_budget_reload_updates_the_reported_limit_without_messaging_the_scheduler()
    {
        var ct = TestContext.Current.CancellationToken;
        _health.UpdateBudget(new BudgetHealthSnapshot(5, 50, RequestBudget.OpenMeteoFreeTierDailyLimit, 300_000));
        var (_, _, scheduler) = await StartManagerAsync();

        await _harness.Writer.UpdateAsync<PollingOptions>(opt => opt.BudgetOverride = new RequestBudget(200_000, 600), ct);

        await AwaitAssertAsync(() =>
        {
            Assert.Equal(200_000, _health.Budget.LimitMonthly);
            Assert.Equal(50, _health.Budget.UsedMonthly);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await scheduler.ExpectNoMsgAsync(Quiet, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_restart_required_reload_sends_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, pipeline, scheduler) = await StartManagerAsync();

        await _harness.Writer.UpdateAsync<NjordOptions>(
            opt => opt.Locations = [new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 }], ct);
        await _harness.Writer.UpdateAsync<AlertOptions>(opt => opt.Enabled = false, ct);

        await pipeline.ExpectNoMsgAsync(Quiet, ct);
        await scheduler.ExpectNoMsgAsync(Quiet, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_reload_from_outside_the_writer_refreshes_the_config_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, pipeline, _) = await StartManagerAsync();
        var stream = await OpenConfigStreamAsync(manager, ct);
        var initial = await stream.ExpectMsgAsync<ConfigSnapshot>(cancellationToken: ct);
        Assert.Null(initial.Analysis.Indices.Preferences.IndoorTemp);

        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(
            _harness.OverridePath,
            """{"Njord": {"Enrichment": {"Indices": {"Preferences": {"IndoorTemp": 19.5}}}}}""",
            ct);
        _harness.Root.Reload();

        await stream.FishForMessageAsync<ConfigSnapshot>(
            s => s.Analysis.Indices.Preferences.IndoorTemp == 19.5, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var changed = await pipeline.ExpectMsgAsync<AnalysisSettingsChanged>(cancellationToken: ct);
        Assert.All(changed.Settings.Indices.Preferences.Values, p => Assert.Equal(19.5, p.IndoorTemp));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Every_options_value_reaches_the_config_snapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = CreateHarness(FullBaseline());
        var (manager, _, _) = await StartManagerAsync(harness);

        var result = Assert.IsType<ConfigResult>(await manager.Ask<ConfigResponse>(new QueryConfig(), AskTimeout, ct));
        var config = result.Config;

        var location = Assert.Single(config.Scope.Locations);
        Assert.Multiple(
            () => Assert.Equal(new LocationId("lucerne"), location.Id),
            () => Assert.Equal(47.05, location.Latitude),
            () => Assert.Equal(8.31, location.Longitude),
            () => Assert.Equal(["icon_eu"], location.Models),
            () => Assert.Equal(["icon_d2", "icon_eu"], location.ResolvedModels),
            () => Assert.Equal(["icon_d2"], config.Scope.Models),
            () => Assert.Equal([6, 24], config.Scope.Horizons),
            () => Assert.Equal(3, config.Scope.ForecastDays),
            () => Assert.Equal(["Weather", "Soil"], config.Scope.Parameters.Groups),
            () => Assert.Equal(["uv_index"], config.Scope.Parameters.Extra),
            () => Assert.Equal(["rain"], config.Scope.Parameters.Exclude),
            () => Assert.Equal(TimeSpan.FromMinutes(90), config.Polling.PollInterval),
            () => Assert.Equal(TimeSpan.FromMinutes(25), config.Polling.DiscoveryInterval),
            () => Assert.Equal(new BudgetLimits(400_000, 500), config.Polling.BudgetOverride),
            () => Assert.Equal(new BudgetLimits(400_000, 500), config.Polling.EffectiveBudget),
            () => Assert.Equal(RequestBudget.OpenMeteoFreeTierDailyLimit, config.Polling.DailyLimit),
            () => Assert.Equal(400_000, config.BudgetProjection.MonthlyLimit));

        var consensus = config.Analysis.Consensus;
        Assert.Equal(new ConsensusSection(false, ConsensusMethod.TrimmedMean, 0.2, 3), consensus);

        var alerts = config.Analysis.Alerts;
        Assert.Multiple(
            () => Assert.False(alerts.Enabled),
            () => Assert.Equal([1.0, -4.0, -14.0], alerts.FrostThresholds),
            () => Assert.Equal([31.0, 36.0, 41.0], alerts.HeatThresholds),
            () => Assert.Equal([18.0, 26.0, 34.0], alerts.StormGustThresholds),
            () => Assert.Equal(11.0, alerts.HeavyRainHourlyThreshold),
            () => Assert.Equal(26.0, alerts.HeavyRainDailyThreshold),
            () => Assert.Equal(6.0, alerts.PressureDropThreshold),
            () => Assert.Equal(11.5, alerts.PressureDropSevereThreshold),
            () => Assert.Equal(5, alerts.FogPersistentHours),
            () => Assert.Equal(1100.0, alerts.CapeThreshold),
            () => Assert.Equal(6.0, alerts.ThunderstormPrecipThreshold),
            () => Assert.Equal(16.0, alerts.ThunderstormGustThreshold),
            () => Assert.Equal(3.0, alerts.IceThreshold),
            () => Assert.Equal([-11.0, -21.0, -31.0], alerts.WindChillThresholds),
            () => Assert.Equal([900.0, 150.0, 40.0], alerts.VisibilityThresholds),
            () => Assert.Equal([21.0, 24.0, 26.0], alerts.TropicalNightThresholds),
            () => Assert.Equal([17.0, 22.0, 25.0], alerts.HumidityThresholds));

        Assert.False(config.Analysis.Derived.Enabled);
        Assert.True(config.Analysis.Trends.Enabled);

        var indices = config.Analysis.Indices;
        var expectedPreferences = new IndexPreferenceValues(21, 4, 19, 11, 1.5, 3.5, 20, 1.1, 1.2, 1.3, 1.4);
        Assert.Multiple(
            () => Assert.True(indices.Enabled),
            () => Assert.Equal(expectedPreferences, indices.Preferences),
            () => Assert.Equal(19.0, indices.ScoreOverrides["Running"].IndoorTemp),
            () => Assert.Equal("lucerne", Assert.Single(indices.LocationOverrides).Location),
            () => Assert.Equal(18.0, indices.LocationOverrides[0].Preferences.IndoorTemp),
            () => Assert.Equal(17.0, indices.LocationOverrides[0].ScoreOverrides["Bbq"].IndoorTemp));
    }

    // Adding an options property must come with a snapshot field: this guard fails until both specs are updated.
    [Fact]
    public void The_snapshot_spec_covers_every_options_property()
    {
        Assert.Multiple(
            () => AssertProperties<NjordOptions>("Locations", "Models", "Horizons", "ForecastDays", "Parameters"),
            () => AssertProperties<LocationOptions>("Name", "Latitude", "Longitude", "Models"),
            () => AssertProperties<ParameterOptions>("Groups", "Extra", "Exclude"),
            () => AssertProperties<PollingOptions>("PollInterval", "DiscoveryInterval", "BudgetOverride"),
            () => AssertProperties<ConsensusOptions>("Enabled", "Method", "TrimPercent", "MinQuorum"),
            () => AssertProperties<AlertOptions>(
                "Enabled", "FrostThresholds", "HeatThresholds", "StormGustThresholds", "HeavyRainHourlyThreshold",
                "HeavyRainDailyThreshold", "PressureDropThreshold", "PressureDropSevereThreshold", "FogPersistentHours",
                "CapeThreshold", "ThunderstormPrecipThreshold", "ThunderstormGustThreshold", "IceThreshold",
                "WindChillThresholds", "VisibilityThresholds", "TropicalNightThresholds", "HumidityThresholds"),
            () => AssertProperties<DerivedOptions>("Enabled"),
            () => AssertProperties<TrendOptions>("Enabled"),
            () => AssertProperties<IndexOptions>("Enabled", "Preferences", "ScoreOverrides", "LocationOverrides"),
            () => AssertProperties<LocationIndexOverride>("Location", "Preferences", "ScoreOverrides"),
            () => AssertProperties<IndexPreferences>(
                "IdealOutdoorTemp", "RunningIdealTempLow", "RunningIdealTempHigh", "BbqMinTemp", "BbqIdealWindLow",
                "BbqIdealWindHigh", "IndoorTemp", "HeatSensitivity", "HumiditySensitivity", "WindSensitivity",
                "RainSensitivity"));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_catalog_lists_locations_with_resolved_models_and_deduplicated_model_coverage()
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = CreateHarness(new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = "lucerne",
            ["Njord:Locations:0:Latitude"] = "47.05",
            ["Njord:Locations:0:Longitude"] = "8.31",
            ["Njord:Locations:1:Name"] = "zurich",
            ["Njord:Locations:1:Latitude"] = "47.37",
            ["Njord:Locations:1:Longitude"] = "8.55",
            ["Njord:Locations:1:Models:0"] = "unknown_model",
            ["Njord:Models:0"] = "icon_d2",
        });
        var (manager, _, _) = await StartManagerAsync(harness);

        var catalog = Assert.IsType<CatalogResult>(await manager.Ask<CatalogResponse>(new QueryCatalog(), AskTimeout, ct));

        Assert.Equal(2, catalog.Locations.Count);
        Assert.Equal(new LocationId("zurich"), catalog.Locations[1].Id);
        Assert.Equal(["icon_d2", "unknown_model"], catalog.Locations[1].Models);
        Assert.Equal(["icon_d2", "unknown_model"], catalog.Models.Select(m => m.Id));
        Assert.Equal(ModelCoverageRegistry.Get("icon_d2"), catalog.Models[0].Coverage);
        Assert.Null(catalog.Models[1].Coverage);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Updating_locations_persists_them_and_reports_the_restart_required_property()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, _, _) = await StartManagerAsync();

        var response = await manager.Ask<UpdateLocationsResponse>(
            new UpdateLocations([new LocationInput("zurich", 47.37, 8.54, ["gfs_seamless"])]), AskTimeout, ct);

        var completed = Assert.IsType<UpdateLocationsCompleted>(response);
        var location = Assert.Single(completed.Config.Scope.Locations);
        Assert.Equal(["icon_d2", "gfs_seamless"], location.ResolvedModels);
        Assert.Equal(["Locations"], completed.RestartRequired);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_empty_location_list_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, _, _) = await StartManagerAsync();

        var response = await manager.Ask<UpdateLocationsResponse>(new UpdateLocations([]), AskTimeout, ct);

        Assert.Equal(new UpdateLocationsRejected("Cannot set empty location list"), response);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_poll_interval_over_the_budget_guard_is_rejected_before_anything_is_persisted()
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = CreateHarness(new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = "lucerne",
            ["Njord:Locations:0:Latitude"] = "47.05",
            ["Njord:Locations:0:Longitude"] = "8.31",
            ["Njord:Models:0"] = "icon_d2",
            ["Njord:BudgetOverride:RequestsPerMonth"] = "100000",
            ["Njord:BudgetOverride:RequestsPerMinute"] = "600",
        });
        var (manager, _, _) = await StartManagerAsync(harness);

        var response = await manager.Ask<UpdateSettingsResponse>(
            new UpdateSettings(null, [], [], null, TimeSpan.FromSeconds(60)), AskTimeout, ct);

        var rejected = Assert.IsType<UpdateSettingsRejected>(response);
        Assert.StartsWith("Would exceed budget", rejected.Reason);
        Assert.False(File.Exists(harness.OverridePath));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_settings_update_validates_before_persisting()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, _, _) = await StartManagerAsync();

        var interval = await manager.Ask<UpdateSettingsResponse>(
            new UpdateSettings(null, [], [], null, TimeSpan.FromSeconds(30)), AskTimeout, ct);
        var days = await manager.Ask<UpdateSettingsResponse>(
            new UpdateSettings(17, [], [], null, null), AskTimeout, ct);

        Assert.Equal(new UpdateSettingsRejected("Poll interval must be at least 60 seconds"), interval);
        Assert.Equal(new UpdateSettingsRejected("Forecast days must be between 1 and 16"), days);
        Assert.False(File.Exists(_harness.OverridePath));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_analysis_update_failing_validation_in_any_section_persists_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, _, _) = await StartManagerAsync();
        var alerts = new AlertConfigUpdate(null, [0, 5, -15], null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null);

        var response = await manager.Ask<UpdateAnalysisConfigResponse>(
            new UpdateAnalysisConfig(new ConsensusConfigUpdate(false, null, null), alerts, null, null, null), AskTimeout, ct);

        var rejected = Assert.IsType<UpdateAnalysisConfigRejected>(response);
        Assert.Contains("FrostThresholds", rejected.Reason);
        Assert.False(File.Exists(_harness.OverridePath));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_analysis_toggle_is_applied_and_reported_as_restart_required()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, _, _) = await StartManagerAsync();

        var response = await manager.Ask<UpdateAnalysisConfigResponse>(
            new UpdateAnalysisConfig(null, null, null, true, null), AskTimeout, ct);

        var completed = Assert.IsType<UpdateAnalysisConfigCompleted>(response);
        Assert.True(completed.Config.Analysis.Trends.Enabled);
        Assert.Equal(["Enrichment.Trends.Enabled"], completed.RestartRequired);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_consensus_toggle_is_applied_and_reported_as_restart_required()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, _, _) = await StartManagerAsync();

        var response = await manager.Ask<UpdateAnalysisConfigResponse>(
            new UpdateAnalysisConfig(new ConsensusConfigUpdate(false, null, null), null, null, null, null), AskTimeout, ct);

        var completed = Assert.IsType<UpdateAnalysisConfigCompleted>(response);
        Assert.False(completed.Config.Analysis.Consensus.Enabled);
        Assert.Equal(["Enrichment.Consensus.Enabled"], completed.RestartRequired);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_budget_update_sets_the_override_and_keeps_the_current_per_minute_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, _, _) = await StartManagerAsync();

        var response = await manager.Ask<UpdateBudgetResponse>(new UpdateBudget(500_000, null), AskTimeout, ct);
        var rejected = await manager.Ask<UpdateBudgetResponse>(new UpdateBudget(null, 0), AskTimeout, ct);

        var completed = Assert.IsType<UpdateBudgetCompleted>(response);
        Assert.Equal(new BudgetLimits(500_000, 600), completed.Config.Polling.BudgetOverride);
        Assert.Empty(completed.RestartRequired);
        Assert.Equal(new UpdateBudgetRejected("requests_per_minute must be greater than zero"), rejected);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_config_stream_starts_with_the_current_snapshot_and_follows_updates()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, _, _) = await StartManagerAsync();
        var stream = await OpenConfigStreamAsync(manager, ct);

        var initial = await stream.ExpectMsgAsync<ConfigSnapshot>(cancellationToken: ct);
        await manager.Ask<UpdateSettingsResponse>(
            new UpdateSettings(null, [], [], null, TimeSpan.FromMinutes(30)), AskTimeout, ct);

        Assert.Equal(TimeSpan.FromMinutes(60), initial.Polling.PollInterval);
        await stream.FishForMessageAsync<ConfigSnapshot>(
            s => s.Polling.PollInterval == TimeSpan.FromMinutes(30), TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    private async Task<TestProbe> OpenConfigStreamAsync(IActorRef manager, CancellationToken ct)
    {
        var response = await manager.Ask<RequestConfigStreamResponse>(new RequestConfigStream(1), AskTimeout, ct);
        var completed = Assert.IsType<RequestConfigStreamCompleted>(response);
        var probe = CreateTestProbe();
        completed.SourceRef.Source.RunWith(Sink.ActorRef<ConfigSnapshot>(probe.Ref, "done", _ => "failed"), Sys.Materializer());
        return probe;
    }

    private static void AssertProperties<T>(params string[] expected) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal), typeof(T).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal));

    private static Dictionary<string, string?> FullBaseline()
    {
        var entries = new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = "lucerne",
            ["Njord:Locations:0:Latitude"] = "47.05",
            ["Njord:Locations:0:Longitude"] = "8.31",
            ["Njord:Locations:0:Models:0"] = "icon_eu",
            ["Njord:Models:0"] = "icon_d2",
            ["Njord:Horizons:0"] = "6",
            ["Njord:Horizons:1"] = "24",
            ["Njord:ForecastDays"] = "3",
            ["Njord:Parameters:Groups:0"] = "Weather",
            ["Njord:Parameters:Groups:1"] = "Soil",
            ["Njord:Parameters:Extra:0"] = "uv_index",
            ["Njord:Parameters:Exclude:0"] = "rain",
            ["Njord:PollInterval"] = "01:30:00",
            ["Njord:DiscoveryInterval"] = "00:25:00",
            ["Njord:BudgetOverride:RequestsPerMonth"] = "400000",
            ["Njord:BudgetOverride:RequestsPerMinute"] = "500",
            ["Njord:Enrichment:Consensus:Enabled"] = "false",
            ["Njord:Enrichment:Consensus:Method"] = "TrimmedMean",
            ["Njord:Enrichment:Consensus:TrimPercent"] = "0.2",
            ["Njord:Enrichment:Consensus:MinQuorum"] = "3",
            ["Njord:Enrichment:Alerts:Enabled"] = "false",
            ["Njord:Enrichment:Alerts:HeavyRainHourlyThreshold"] = "11",
            ["Njord:Enrichment:Alerts:HeavyRainDailyThreshold"] = "26",
            ["Njord:Enrichment:Alerts:PressureDropThreshold"] = "6",
            ["Njord:Enrichment:Alerts:PressureDropSevereThreshold"] = "11.5",
            ["Njord:Enrichment:Alerts:FogPersistentHours"] = "5",
            ["Njord:Enrichment:Alerts:CapeThreshold"] = "1100",
            ["Njord:Enrichment:Alerts:ThunderstormPrecipThreshold"] = "6",
            ["Njord:Enrichment:Alerts:ThunderstormGustThreshold"] = "16",
            ["Njord:Enrichment:Alerts:IceThreshold"] = "3",
            ["Njord:Enrichment:Derived:Enabled"] = "false",
            ["Njord:Enrichment:Trends:Enabled"] = "true",
            ["Njord:Enrichment:Indices:Enabled"] = "true",
            ["Njord:Enrichment:Indices:ScoreOverrides:Running:IndoorTemp"] = "19",
            ["Njord:Enrichment:Indices:LocationOverrides:0:Location"] = "lucerne",
            ["Njord:Enrichment:Indices:LocationOverrides:0:Preferences:IndoorTemp"] = "18",
            ["Njord:Enrichment:Indices:LocationOverrides:0:ScoreOverrides:Bbq:IndoorTemp"] = "17",
        };
        AddList(entries, "Njord:Enrichment:Alerts:FrostThresholds", 1, -4, -14);
        AddList(entries, "Njord:Enrichment:Alerts:HeatThresholds", 31, 36, 41);
        AddList(entries, "Njord:Enrichment:Alerts:StormGustThresholds", 18, 26, 34);
        AddList(entries, "Njord:Enrichment:Alerts:WindChillThresholds", -11, -21, -31);
        AddList(entries, "Njord:Enrichment:Alerts:VisibilityThresholds", 900, 150, 40);
        AddList(entries, "Njord:Enrichment:Alerts:TropicalNightThresholds", 21, 24, 26);
        AddList(entries, "Njord:Enrichment:Alerts:HumidityThresholds", 17, 22, 25);

        var preferences = new (string Name, double Value)[]
        {
            ("IdealOutdoorTemp", 21), ("RunningIdealTempLow", 4), ("RunningIdealTempHigh", 19), ("BbqMinTemp", 11),
            ("BbqIdealWindLow", 1.5), ("BbqIdealWindHigh", 3.5), ("IndoorTemp", 20), ("HeatSensitivity", 1.1),
            ("HumiditySensitivity", 1.2), ("WindSensitivity", 1.3), ("RainSensitivity", 1.4),
        };
        foreach (var (name, value) in preferences)
        {
            entries[$"Njord:Enrichment:Indices:Preferences:{name}"] = value.ToString(CultureInfo.InvariantCulture);
        }

        return entries;
    }

    private static void AddList(Dictionary<string, string?> entries, string key, params double[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            entries[$"{key}:{i}"] = values[i].ToString(CultureInfo.InvariantCulture);
        }
    }

    protected override async Task AfterAllAsync()
    {
        await base.AfterAllAsync();
        foreach (var harness in _harnesses)
        {
            harness.Dispose();
        }

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
