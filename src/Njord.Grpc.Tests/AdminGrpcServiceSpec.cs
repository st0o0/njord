using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
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
using Njord.Grpc.V2;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class AdminGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-test-{Guid.NewGuid():N}");
    private readonly List<ConfigHarness> _harnesses = [];

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetConfig_returns_current_configuration()
    {
        var service = CreateService();

        var config = await service.GetConfig(new GetConfigRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var location = Assert.Single(config.Locations);
        Assert.Equal("lucerne", location.Name);
        Assert.Equal(47.05, location.Latitude);
        Assert.Equal(8.31, location.Longitude);
        Assert.Equal(new[] { "icon_d2" }, config.DefaultModels);
        Assert.Equal(new[] { "icon_d2" }, location.Models);
        Assert.Contains(3, config.Horizons);
        Assert.Contains(72, config.Horizons);
        Assert.Equal(4, config.ForecastDays);
        Assert.Equal(3600, config.PollIntervalSeconds);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetLocations_replaces_all_locations()
    {
        var service = CreateService();
        var request = new SetLocationsRequest
        {
            Locations =
            {
                new LocationInput { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
                new LocationInput { Name = "bern", Latitude = 46.95, Longitude = 7.44, Models = { "gfs_seamless" } },
            },
        };

        var response = await service.SetLocations(request, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        Assert.Equal(2, response.Config.Locations.Count);
        Assert.DoesNotContain(response.Config.Locations, l => l.Name == "lucerne");
        Assert.Contains(response.Config.Locations, l => l.Name == "zurich");

        var bern = response.Config.Locations.First(l => l.Name == "bern");
        Assert.Equal(new[] { "icon_d2", "gfs_seamless" }, bern.Models);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetLocations_rejects_empty_list()
    {
        var service = CreateService();

        var response = await service.SetLocations(new SetLocationsRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Equal("Cannot set empty location list", response.RejectionReason);
        Assert.Null(response.Config);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetSettings_applies_partial_update()
    {
        var service = CreateService();

        var response = await service.SetSettings(
            new SetSettingsRequest { PollIntervalSeconds = 1800 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        Assert.Equal(1800, response.Config.PollIntervalSeconds);
        Assert.Equal(4, response.Config.ForecastDays);
        Assert.Contains(3, response.Config.Horizons);
        Assert.Contains(72, response.Config.Horizons);
        Assert.Equal(new[] { "icon_d2" }, response.Config.DefaultModels);
        Assert.Single(response.Config.Locations);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetSettings_rejects_poll_interval_below_minimum()
    {
        var service = CreateService();

        var response = await service.SetSettings(
            new SetSettingsRequest { PollIntervalSeconds = 30 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Equal("Poll interval must be at least 60 seconds", response.RejectionReason);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetBudget_sets_override()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = 500_000 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        Assert.NotNull(response.Config.BudgetOverride);
        Assert.Equal(500_000, response.Config.BudgetOverride.RequestsPerMonth);
        Assert.Equal(600, response.Config.BudgetOverride.RequestsPerMinute);
        Assert.Equal(500_000, response.BudgetProjection.MonthlyLimit);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetBudget_clears_override_when_empty()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var setResponse = await service.SetBudget(new SetBudgetRequest { RequestsPerMonth = 100_000 }, ctx);
        Assert.True(setResponse.Applied);
        Assert.NotNull(setResponse.Config.BudgetOverride);

        var clearResponse = await service.SetBudget(new SetBudgetRequest(), ctx);
        Assert.True(clearResponse.Applied);

        var config = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.True(config.BudgetProjection.WithinBudget);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetBudget_rejects_zero_monthly_budget()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = 0 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Contains("must be greater than zero", response.RejectionReason);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetBudget_rejects_negative_monthly_budget()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = -100 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Contains("must be greater than zero", response.RejectionReason);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetBudget_rejects_zero_per_minute_budget()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMinute = 0 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Contains("must be greater than zero", response.RejectionReason);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetBudget_rejects_when_valid_monthly_but_zero_per_minute()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = 300_000, RequestsPerMinute = 0 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Contains("must be greater than zero", response.RejectionReason);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_disable_alerts_is_reflected_in_GetConfig()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { Enabled = false } }, ctx);

        Assert.True(response.Applied);
        Assert.False(response.Config.Enrichment.Alerts.Enabled);

        var config = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.False(config.Enrichment.Alerts.Enabled);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_with_trimmed_mean_sets_the_method()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Consensus = new ConsensusConfig { Method = ConsensusMethod.TrimmedMean } }, ctx);

        Assert.True(response.Applied);
        Assert.Equal(ConsensusMethod.TrimmedMean, response.Config.Enrichment.Consensus.Method);
        var config = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.Equal(ConsensusMethod.TrimmedMean, config.Enrichment.Consensus.Method);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_with_unspecified_method_leaves_the_method_unchanged()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);
        var before = await service.GetConfig(new GetConfigRequest(), ctx);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Consensus = new ConsensusConfig { Method = ConsensusMethod.Unspecified } }, ctx);

        Assert.True(response.Applied);
        Assert.Equal(before.Enrichment.Consensus.Method, response.Config.Enrichment.Consensus.Method);
        Assert.NotEqual(ConsensusMethod.Unspecified, response.Config.Enrichment.Consensus.Method);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_with_an_undefined_method_is_rejected_and_changes_nothing()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);
        var before = await service.GetConfig(new GetConfigRequest(), ctx);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Consensus = new ConsensusConfig { Method = (ConsensusMethod)42 } }, ctx);

        Assert.False(response.Applied);
        Assert.Equal("Unknown consensus method: 42", response.RejectionReason);
        var after = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.Equal(before.Enrichment.Consensus, after.Enrichment.Consensus);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_overrides_baseline_config()
    {
        var service = CreateService(alertsEnabled: true);
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var configBefore = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.True(configBefore.Enrichment.Alerts.Enabled);

        await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { Enabled = false } }, ctx);

        var configAfter = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.False(configAfter.Enrichment.Alerts.Enabled);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetLocations_reports_that_the_change_takes_effect_after_a_restart()
    {
        var service = CreateService();
        var request = new SetLocationsRequest
        {
            Locations = { new LocationInput { Name = "zurich", Latitude = 47.37, Longitude = 8.54 } },
        };

        var response = await service.SetLocations(request, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        var notice = Assert.Single(response.RestartRequired);
        Assert.Contains("Locations", notice);
        Assert.Empty(response.Warnings);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_with_only_alert_thresholds_needs_no_restart()
    {
        var service = CreateService();

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { FrostThresholds = { 1, -4, -12 } } },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        Assert.Empty(response.RestartRequired);
        Assert.Equal(new[] { 1.0, -4.0, -12.0 }, response.Config.Enrichment.Alerts.FrostThresholds);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_toggle_reports_that_it_takes_effect_after_a_restart()
    {
        var service = CreateService();

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Trends = new TrendConfig { Enabled = true } },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        Assert.Equal(new[] { "Enrichment.Trends.Enabled" }, response.RestartRequired);
        Assert.DoesNotContain(response.Warnings, w => w.Contains("restart", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_with_non_monotonic_frost_thresholds_is_rejected_and_not_persisted()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);
        await service.SetSettings(new SetSettingsRequest { ForecastDays = 5 }, ctx);
        var before = await File.ReadAllTextAsync(OverridePath(), TestContext.Current.CancellationToken);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { FrostThresholds = { 0, 5, -15 } } }, ctx);

        Assert.False(response.Applied);
        Assert.Contains("FrostThresholds", response.RejectionReason);
        Assert.Equal(before, await File.ReadAllTextAsync(OverridePath(), TestContext.Current.CancellationToken));
        var config = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.Equal(new[] { 0.0, -5.0, -15.0 }, config.Enrichment.Alerts.FrostThresholds);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_rejection_carries_a_structured_issue_with_the_section()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { FrostThresholds = { 0, 5, -15 } } }, ctx);

        var issue = Assert.Single(response.Issues);
        Assert.Multiple(
            () => Assert.False(response.Applied),
            () => Assert.False(response.DryRun),
            () => Assert.Equal(EnrichmentSection.Alerts, issue.Section),
            () => Assert.Contains("FrostThresholds", issue.Message),
            () => Assert.Equal(issue.Message, response.RejectionReason));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_without_any_section_is_rejected()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var response = await service.SetEnrichment(new SetEnrichmentRequest(), ctx);

        var issue = Assert.Single(response.Issues);
        Assert.Multiple(
            () => Assert.False(response.Applied),
            () => Assert.Equal(EnrichmentSection.Unspecified, issue.Section),
            () => Assert.Equal("At least one section or reset is required", issue.Message),
            () => Assert.Equal(issue.Message, response.RejectionReason),
            () => Assert.False(File.Exists(OverridePath())));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_dry_run_validates_without_persisting()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var valid = await service.SetEnrichment(
            new SetEnrichmentRequest { Trends = new TrendConfig { Enabled = true }, DryRun = true }, ctx);
        var invalid = await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { FrostThresholds = { 0, 5, -15 } }, DryRun = true }, ctx);

        Assert.Multiple(
            () => Assert.False(valid.Applied),
            () => Assert.True(valid.DryRun),
            () => Assert.Empty(valid.Issues),
            () => Assert.Empty(valid.RejectionReason),
            () => Assert.Equal(new[] { "Enrichment.Trends.Enabled" }, valid.RestartRequired),
            () => Assert.False(valid.Config.Enrichment.Trends.Enabled),
            () => Assert.False(invalid.Applied),
            () => Assert.True(invalid.DryRun),
            () => Assert.Equal(EnrichmentSection.Alerts, Assert.Single(invalid.Issues).Section),
            () => Assert.False(File.Exists(OverridePath())));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_with_an_unspecified_reset_section_is_reported_as_an_issue()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest
            {
                Alerts = new AlertConfig { Enabled = false },
                Reset = { EnrichmentSection.Unspecified, (EnrichmentSection)42 },
                DryRun = true,
            },
            ctx);

        Assert.Multiple(
            () => Assert.False(response.Applied),
            () => Assert.True(response.DryRun),
            () => Assert.Equal(
                new[] { "Unknown or unspecified section in reset: 0", "Unknown or unspecified section in reset: 42" },
                response.Issues.Select(i => i.Message)),
            () => Assert.All(response.Issues, i => Assert.Equal(EnrichmentSection.Unspecified, i.Section)),
            () => Assert.False(File.Exists(OverridePath())));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_reset_restores_the_base_configuration_and_applies_the_update_on_top()
    {
        var service = CreateService(alertsEnabled: true);
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);
        await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { CapeThreshold = 1800, HeatThresholds = { 31, 36, 41 } } }, ctx);
        var defaults = AppSettingsDefaults.Options<AlertOptions>();

        var reset = await service.SetEnrichment(new SetEnrichmentRequest { Reset = { EnrichmentSection.Alerts } }, ctx);
        var resetAndDisable = await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { Enabled = false }, Reset = { EnrichmentSection.Alerts } }, ctx);

        Assert.Multiple(
            () => Assert.True(reset.Applied),
            () => Assert.Equal(defaults.CapeThreshold, reset.Config.Enrichment.Alerts.CapeThreshold),
            () => Assert.Equal(defaults.HeatThresholds, reset.Config.Enrichment.Alerts.HeatThresholds),
            () => Assert.True(reset.Config.Enrichment.Alerts.Enabled),
            () => Assert.True(resetAndDisable.Applied),
            () => Assert.False(resetAndDisable.Config.Enrichment.Alerts.Enabled),
            () => Assert.Equal(new[] { "Enrichment.Alerts.Enabled" }, resetAndDisable.RestartRequired));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetEnrichment_with_an_undefined_method_is_reported_as_a_consensus_issue()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Consensus = new ConsensusConfig { Method = (ConsensusMethod)42 } }, ctx);

        var issue = Assert.Single(response.Issues);
        Assert.Multiple(
            () => Assert.Equal(EnrichmentSection.Consensus, issue.Section),
            () => Assert.Equal("Unknown consensus method: 42", issue.Message));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task SetSettings_rejects_a_poll_interval_over_the_budget_guard_and_keeps_the_previous_one()
    {
        var service = CreateService(budgetPerMonth: 100_000);
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var response = await service.SetSettings(new SetSettingsRequest { PollIntervalSeconds = 60 }, ctx);

        Assert.False(response.Applied);
        Assert.Contains("budget", response.RejectionReason);
        Assert.False(File.Exists(OverridePath()));
        var config = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.Equal(3600, config.PollIntervalSeconds);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamConfig_sends_a_new_snapshot_after_a_mutation()
    {
        var service = CreateService();
        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var writer = new RecordingStreamWriter();

        var stream = service.StreamConfig(new StreamConfigRequest(), writer, TestServerCallContext.Create(streamCts.Token));
        await writer.WaitForCount(1).WaitAsync(TestContext.Current.CancellationToken);

        await service.SetSettings(
            new SetSettingsRequest { PollIntervalSeconds = 1800 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));
        await writer.WaitForCount(2).WaitAsync(TestContext.Current.CancellationToken);

        await streamCts.CancelAsync();
        await stream.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3600, writer.Messages[0].PollIntervalSeconds);
        Assert.Equal(1800, writer.Messages[1].PollIntervalSeconds);
    }

    private sealed class RecordingStreamWriter : global::Grpc.Core.IServerStreamWriter<NjordConfig>
    {
        private readonly object _gate = new();
        private readonly List<NjordConfig> _messages = [];
        private readonly List<(int Count, TaskCompletionSource Signal)> _waiters = [];

        public global::Grpc.Core.WriteOptions? WriteOptions { get; set; }

        public IReadOnlyList<NjordConfig> Messages
        {
            get
            {
                lock (_gate)
                {
                    return [.. _messages];
                }
            }
        }

        public Task WriteAsync(NjordConfig message)
        {
            lock (_gate)
            {
                _messages.Add(message);
                foreach (var waiter in _waiters.Where(w => _messages.Count >= w.Count))
                {
                    waiter.Signal.TrySetResult();
                }
            }

            return Task.CompletedTask;
        }

        public Task WaitForCount(int count)
        {
            lock (_gate)
            {
                if (_messages.Count >= count)
                {
                    return Task.CompletedTask;
                }

                var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((count, signal));
                return signal.Task;
            }
        }
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
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

    private string OverridePath() => Path.Combine(_tempDir, "appsettings.Override.json");

    private AdminGrpcService CreateService(bool alertsEnabled = true, int? budgetPerMonth = null)
    {
        var configEntries = new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = "lucerne",
            ["Njord:Locations:0:Latitude"] = "47.05",
            ["Njord:Locations:0:Longitude"] = "8.31",
            ["Njord:Models:0"] = "icon_d2",
            ["Njord:Enrichment:Alerts:Enabled"] = alertsEnabled.ToString(),
            ["Njord:ForecastDays"] = "4",
        };
        if (budgetPerMonth is { } perMonth)
        {
            configEntries["Njord:Polling:BudgetOverride:RequestsPerMonth"] = perMonth.ToString();
            configEntries["Njord:Polling:BudgetOverride:RequestsPerMinute"] = "600";
        }

        var harness = new ConfigHarness(configEntries, OverridePath());
        _harnesses.Add(harness);

        ActorRegistry.Register<IConfigManager>(StartConfigManager(harness), overwrite: true);
        return new AdminGrpcService(ActorRegistry, Sys);
    }

    private IActorRef StartConfigManager(ConfigHarness h)
    {
        var startup = AnalysisSettingsFactory.Create(
            h.Monitor<NjordOptions>().CurrentValue,
            h.Monitor<ConsensusOptions>().CurrentValue,
            h.Monitor<AlertOptions>().CurrentValue,
            h.Monitor<IndexOptions>().CurrentValue,
            h.Monitor<TrendOptions>().CurrentValue,
            h.Monitor<DerivedOptions>().CurrentValue);

        return Sys.ActorOf(Props.Create(() => new ConfigManager(
            Options.Create(h.Monitor<NjordOptions>().CurrentValue),
            startup,
            h.Monitor<NjordOptions>(),
            h.Monitor<PollingOptions>(),
            h.Monitor<ConsensusOptions>(),
            h.Monitor<AlertOptions>(),
            h.Monitor<IndexOptions>(),
            h.Monitor<TrendOptions>(),
            h.Monitor<DerivedOptions>(),
            h.Writer,
            new NjordHealthMonitor(default, new BudgetHealthSnapshot(0, 0, 0, 0)))), $"config-manager-{Guid.NewGuid():N}");
    }
}
