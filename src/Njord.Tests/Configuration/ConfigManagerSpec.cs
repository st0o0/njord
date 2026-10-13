using Akka.Actor;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Njord.Compute.Consensus;
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
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Tests.Configuration;

public sealed class ConfigManagerSpec : NjordActorSpec
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-config-manager-{Guid.NewGuid():N}");
    private readonly ConfigHarness _harness;
    private readonly NjordHealthMonitor _health = new();

    protected override bool UseTestPersistence => false;

    public ConfigManagerSpec()
    {
        _harness = new ConfigHarness(new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = "lucerne",
            ["Njord:Locations:0:Latitude"] = "47.05",
            ["Njord:Locations:0:Longitude"] = "8.31",
            ["Njord:Models:0"] = "icon_d2",
        }, Path.Combine(_tempDir, "appsettings.Override.json"));
    }

    private async Task<(TestProbe Pipeline, TestProbe Scheduler, ConfigChangeFeed Feed)> StartManagerAsync()
    {
        var pipeline = CreateTestProbe();
        var scheduler = CreateTestProbe();
        ActorRegistry.Register<IForecastPipeline>(pipeline.Ref, overwrite: true);
        ActorRegistry.Register<IPollScheduler>(scheduler.Ref, overwrite: true);

        var h = _harness;
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
            h.Changes,
            _health)), $"config-manager-{Guid.NewGuid():N}");

        // PreStart (where the change subscriptions are made) has run once the actor answers.
        await manager.Ask<ActorIdentity>(new Identify(1), TimeSpan.FromSeconds(9), TestContext.Current.CancellationToken);
        return (pipeline, scheduler, h.Changes);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_alert_threshold_reload_reaches_the_pipeline()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, scheduler, _) = await StartManagerAsync();

        await _harness.Writer.UpdateAsync<AlertOptions>(opt => opt.FrostThresholds = [1, -4, -12], ct);

        var changed = await pipeline.ExpectMsgAsync<AnalysisSettingsChanged>(cancellationToken: ct);
        Assert.Equal(new SeverityThresholds(1, -4, -12, ScanDirection.Min), changed.Settings.Alerts.Frost);
        await scheduler.ExpectNoMsgAsync(Quiet, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_poll_interval_reload_reaches_the_scheduler()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, scheduler, _) = await StartManagerAsync();

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
        var (_, scheduler, _) = await StartManagerAsync();

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
        var (pipeline, scheduler, _) = await StartManagerAsync();

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
        var (pipeline, _, feed) = await StartManagerAsync();
        var signalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = feed.Subscribe(() => signalled.TrySetResult());

        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(
            _harness.OverridePath,
            """{"Njord": {"Enrichment": {"Indices": {"Preferences": {"IndoorTemp": 19.5}}}}}""",
            ct);
        _harness.Root.Reload();

        await signalled.Task.WaitAsync(TestTimeouts.AwaitAssertMax, ct);
        var changed = await pipeline.ExpectMsgAsync<AnalysisSettingsChanged>(cancellationToken: ct);
        Assert.All(changed.Settings.Indices.Preferences.Values, p => Assert.Equal(19.5, p.IndoorTemp));
    }

    protected override async Task AfterAllAsync()
    {
        await base.AfterAllAsync();
        _harness.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
