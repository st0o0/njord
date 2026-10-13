using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Njord.Compute.Settings;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;
using Njord.Core.Health;

namespace Njord.Configuration;

// The only subscriber to configuration change notifications. A reload is turned into typed messages for
// the actors that consume hot settings; restart-required sections only refresh the admin config stream.
public sealed class ConfigManager : ReceiveActor
{
    private sealed record ConfigReloaded;

    private readonly NjordOptions _startupScope;
    private readonly IOptionsMonitor<NjordOptions> _scope;
    private readonly IOptionsMonitor<PollingOptions> _polling;
    private readonly IOptionsMonitor<ConsensusOptions> _consensus;
    private readonly IOptionsMonitor<AlertOptions> _alerts;
    private readonly IOptionsMonitor<IndexOptions> _indices;
    private readonly IOptionsMonitor<TrendOptions> _trends;
    private readonly IOptionsMonitor<DerivedOptions> _derived;
    private readonly ConfigChangeFeed _changes;
    private readonly NjordHealthMonitor _health;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly List<IDisposable> _subscriptions = [];

    private AnalysisSettings _analysis;
    private PollingChanged _pollingState;
    private RequestBudget _budget;

    public ConfigManager(
        IOptions<NjordOptions> startupScope,
        AnalysisSettings startupSettings,
        IOptions<PollingOptions> startupPolling,
        IOptionsMonitor<NjordOptions> scope,
        IOptionsMonitor<PollingOptions> polling,
        IOptionsMonitor<ConsensusOptions> consensus,
        IOptionsMonitor<AlertOptions> alerts,
        IOptionsMonitor<IndexOptions> indices,
        IOptionsMonitor<TrendOptions> trends,
        IOptionsMonitor<DerivedOptions> derived,
        ConfigChangeFeed changes,
        NjordHealthMonitor health)
    {
        _startupScope = startupScope.Value;
        _analysis = startupSettings;
        _pollingState = ToMessage(startupPolling.Value);
        _budget = BudgetCalculator.GetEffectiveBudget(startupPolling.Value);
        _scope = scope;
        _polling = polling;
        _consensus = consensus;
        _alerts = alerts;
        _indices = indices;
        _trends = trends;
        _derived = derived;
        _changes = changes;
        _health = health;

        Receive<ConfigReloaded>(_ => OnReloaded());
    }

    protected override void PreStart()
    {
        var self = Self;
        Subscribe(_scope, self);
        Subscribe(_polling, self);
        Subscribe(_consensus, self);
        Subscribe(_alerts, self);
        Subscribe(_indices, self);
        Subscribe(_trends, self);
        Subscribe(_derived, self);
    }

    // Change callbacks run on a configuration thread: they only tell this actor.
    private void Subscribe<T>(IOptionsMonitor<T> monitor, IActorRef self)
    {
        if (monitor.OnChange(_ => self.Tell(new ConfigReloaded())) is { } subscription)
        {
            _subscriptions.Add(subscription);
        }
    }

    protected override void PostStop()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        base.PostStop();
    }

    private void OnReloaded()
    {
        try
        {
            ApplyAnalysis(AnalysisSettingsFactory.Create(
                _startupScope,
                _consensus.CurrentValue,
                _alerts.CurrentValue,
                _indices.CurrentValue,
                _trends.CurrentValue,
                _derived.CurrentValue));
            ApplyPolling(ToMessage(_polling.CurrentValue));
            ApplyBudget(BudgetCalculator.GetEffectiveBudget(_polling.CurrentValue));
        }
        catch (OptionsValidationException ex)
        {
            // Only a hand-edited file can get here: runtime updates are validated before they are written.
            _log.Warning("Reloaded configuration is invalid and was not applied: {Failures}", string.Join("; ", ex.Failures));
        }

        _changes.Publish();
    }

    private void ApplyAnalysis(AnalysisSettings next)
    {
        if (Equivalent(_analysis, next))
        {
            return;
        }

        _analysis = next;
        Send<IForecastPipeline>(new AnalysisSettingsChanged(next));
        _log.Info("Analysis settings reloaded");
    }

    private void ApplyPolling(PollingChanged next)
    {
        if (next == _pollingState)
        {
            return;
        }

        _pollingState = next;
        Send<IPollScheduler>(next);
        _log.Info("Polling reloaded: poll interval {PollInterval}, discovery interval {DiscoveryInterval}",
            next.PollInterval, next.DiscoveryInterval);
    }

    // The gateway's budget gate reads the budget per call; the health snapshot behind the budget metrics
    // is refreshed here so the reported limit matches.
    private void ApplyBudget(RequestBudget next)
    {
        if (next == _budget)
        {
            return;
        }

        _budget = next;
        _health.UpdateBudget(_health.Budget with { LimitMonthly = next.RequestsPerMonth });
        _log.Info("Request budget reloaded: {PerMonth}/month, {PerMinute}/min", next.RequestsPerMonth, next.RequestsPerMinute);
    }

    private void Send<TKey>(object message)
    {
        if (ActorRegistry.For(Context.System).TryGet<TKey>(out var target))
        {
            target.Tell(message);
        }
        else
        {
            _log.Warning("{Target} is not registered; {Message} was not delivered", typeof(TKey).Name, message.GetType().Name);
        }
    }

    private static PollingChanged ToMessage(PollingOptions polling) =>
        new(polling.PollInterval, polling.DiscoveryInterval);

    private static bool Equivalent(AnalysisSettings a, AnalysisSettings b) =>
        a.Consensus == b.Consensus
        && a.Alerts == b.Alerts
        && a.Trends == b.Trends
        && a.Derived.Horizons.SequenceEqual(b.Derived.Horizons)
        && a.Indices.Preferences.Count == b.Indices.Preferences.Count
        && a.Indices.Preferences.All(p =>
            b.Indices.Preferences.TryGetValue(p.Key, out var other) && other == p.Value);
}
