using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Messages.Config;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;
using Njord.Weather;

namespace Njord.Configuration;

// The only subscriber to configuration change notifications and the only writer of runtime configuration.
// A reload is turned into typed messages for the actors that consume hot settings and a new effective config on the
// config stream; queries and update commands from the API are answered with configuration contracts.
public sealed class ConfigManager : ReceiveActor
{
    private const int StreamBufferSize = 16;

    private sealed record ConfigReloaded;

    private readonly NjordOptions _startupScope;
    private readonly IOptionsMonitor<NjordOptions> _scope;
    private readonly IOptionsMonitor<PollingOptions> _polling;
    private readonly IOptionsMonitor<ConsensusOptions> _consensus;
    private readonly IOptionsMonitor<AlertOptions> _alerts;
    private readonly IOptionsMonitor<IndexOptions> _indices;
    private readonly IOptionsMonitor<TrendOptions> _trends;
    private readonly IOptionsMonitor<DerivedOptions> _derived;
    private readonly IConfigWriter _writer;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly List<IDisposable> _subscriptions = [];
    private readonly CancellationTokenSource _lifetime = new();

    private AnalysisSettings _analysis;
    private PollingChanged _pollingState;
    private RequestBudget _budget;
    private IMaterializer? _mat;
    private ISourceQueueWithComplete<EffectiveConfig>? _streamQueue;
    private Source<EffectiveConfig, NotUsed>? _streamSource;

    public ConfigManager(
        IOptions<NjordOptions> startupScope,
        IOptionsMonitor<NjordOptions> scope,
        IOptionsMonitor<PollingOptions> polling,
        IOptionsMonitor<ConsensusOptions> consensus,
        IOptionsMonitor<AlertOptions> alerts,
        IOptionsMonitor<IndexOptions> indices,
        IOptionsMonitor<TrendOptions> trends,
        IOptionsMonitor<DerivedOptions> derived,
        IConfigWriter writer)
    {
        _startupScope = startupScope.Value;
        _analysis = AnalysisSettingsFactory.Create(
            _startupScope, consensus.CurrentValue, alerts.CurrentValue, indices.CurrentValue, trends.CurrentValue, derived.CurrentValue);
        _pollingState = polling.CurrentValue.ToMessage();
        _budget = BudgetCalculator.GetEffectiveBudget(polling.CurrentValue);
        _scope = scope;
        _polling = polling;
        _consensus = consensus;
        _alerts = alerts;
        _indices = indices;
        _trends = trends;
        _derived = derived;
        _writer = writer;

        Receive<ConfigReloaded>(_ => OnReloaded());
        Receive<QueryConfig>(_ => OnQueryConfig());
        Receive<QueryAnalysisSettings>(_ => Sender.Tell(new AnalysisSettingsResult(_analysis)));
        Receive<QueryCatalog>(_ => OnQueryCatalog());
        Receive<RequestConfigStream>(OnRequestConfigStream);
        ReceiveAsync<UpdateLocations>(OnUpdateLocations);
        ReceiveAsync<UpdateSettings>(OnUpdateSettings);
        ReceiveAsync<UpdateAnalysisConfig>(OnUpdateAnalysisConfig);
        ReceiveAsync<UpdateBudget>(OnUpdateBudget);
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

        // The drain keeps the hub moving while no StreamConfig client is attached.
        _mat = Context.Materializer();
        var (queue, source) = Source.Queue<EffectiveConfig>(StreamBufferSize, OverflowStrategy.DropHead)
            .ToMaterialized(BroadcastHub.Sink<EffectiveConfig>(StreamBufferSize), Keep.Both)
            .Run(_mat);
        source.RunWith(Sink.Ignore<EffectiveConfig>(), _mat);
        _streamQueue = queue;
        _streamSource = source;
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

        _streamQueue?.Complete();
        _lifetime.Cancel();
        _lifetime.Dispose();
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
            ApplyPolling(_polling.CurrentValue.ToMessage());
            ApplyBudget(BudgetCalculator.GetEffectiveBudget(_polling.CurrentValue));
            _streamQueue?.OfferAsync(CurrentConfig());
        }
        catch (OptionsValidationException ex)
        {
            // Only a hand-edited file can get here: runtime updates are validated before they are written.
            _log.Warning("Reloaded configuration is invalid and was not applied: {Failures}", string.Join("; ", ex.Failures));
        }
        catch (ParameterResolutionException ex)
        {
            _log.Warning("Reloaded configuration has invalid parameters and was not published: {Errors}", string.Join("; ", ex.Errors));
        }
    }

    private void OnQueryConfig()
    {
        try
        {
            Sender.Tell(new ConfigResult(CurrentConfig()));
        }
        catch (Exception ex)
        {
            Sender.Tell(new ConfigFailed(ex));
        }
    }

    // The catalog is what the running service serves: the forecast scope it started with.
    private void OnQueryCatalog()
    {
        try
        {
            Sender.Tell(_startupScope.ToCatalog());
        }
        catch (Exception ex)
        {
            Sender.Tell(new CatalogFailed(ex));
        }
    }

    // Each subscriber starts with the current config; later configs come from the hub, which the
    // subscriber joins here, in the same turn, so no reload between the two is lost.
    private void OnRequestConfigStream(RequestConfigStream msg)
    {
        EffectiveConfig current;
        try
        {
            current = CurrentConfig();
        }
        catch (Exception ex)
        {
            Sender.Tell(new RequestConfigStreamFailed(msg.RequestId, ex));
            return;
        }

        Source.Single(current)
            .Concat(_streamSource!)
            .RunWith(StreamRefs.SourceRef<EffectiveConfig>(), _mat!)
            .PipeTo(Sender, Self,
                success: sourceRef => new RequestConfigStreamCompleted(msg.RequestId, sourceRef),
                failure: ex => new RequestConfigStreamFailed(msg.RequestId, ex));
    }

    private async Task OnUpdateLocations(UpdateLocations cmd)
    {
        var sender = Sender;
        try
        {
            sender.Tell(await UpdateLocationsAsync(cmd));
        }
        catch (Exception ex)
        {
            sender.Tell(new UpdateLocationsFailed(ex));
        }
    }

    private async Task<UpdateLocationsResponse> UpdateLocationsAsync(UpdateLocations cmd)
    {
        if (cmd.Locations.Count == 0)
        {
            return new UpdateLocationsRejected("Cannot set empty location list");
        }

        List<LocationOptions> locations =
        [
            .. cmd.Locations.Select(l => new LocationOptions
            {
                Name = l.Name,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Models = l.Models.Count > 0 ? [.. l.Models] : null,
            })
        ];

        if (BudgetRejection(scope => scope.Locations = locations, _polling.CurrentValue) is { } budgetRejection)
        {
            return new UpdateLocationsRejected(budgetRejection);
        }

        var result = await _writer.UpdateAsync<NjordOptions>(opt => opt.Locations = locations, _lifetime.Token);
        return result.Applied
            ? new UpdateLocationsCompleted(CurrentConfig(), [.. RestartRequired(result, "")])
            : new UpdateLocationsRejected(string.Join("; ", result.Failures));
    }

    private async Task OnUpdateSettings(UpdateSettings cmd)
    {
        var sender = Sender;
        try
        {
            sender.Tell(await UpdateSettingsAsync(cmd));
        }
        catch (Exception ex)
        {
            sender.Tell(new UpdateSettingsFailed(ex));
        }
    }

    private async Task<UpdateSettingsResponse> UpdateSettingsAsync(UpdateSettings cmd)
    {
        if (cmd.PollInterval < TimeSpan.FromSeconds(60))
        {
            return new UpdateSettingsRejected("Poll interval must be at least 60 seconds");
        }

        if (cmd.ForecastDays is < 1 or > 16)
        {
            return new UpdateSettingsRejected("Forecast days must be between 1 and 16");
        }

        var polling = ConfigWriter.DeepClone(_polling.CurrentValue);
        if (cmd.PollInterval is { } interval)
        {
            polling.PollInterval = interval;
        }

        if (BudgetRejection(scope => cmd.ApplyTo(scope), polling) is { } budgetRejection)
        {
            return new UpdateSettingsRejected(budgetRejection);
        }

        var failures = _writer.Check<NjordOptions>(opt => cmd.ApplyTo(opt));
        if (failures.Count > 0)
        {
            return new UpdateSettingsRejected(string.Join("; ", failures));
        }

        var scopeResult = await _writer.UpdateAsync<NjordOptions>(opt => cmd.ApplyTo(opt), _lifetime.Token);
        if (!scopeResult.Applied)
        {
            return new UpdateSettingsRejected(string.Join("; ", scopeResult.Failures));
        }

        if (cmd.PollInterval is not null)
        {
            var pollingResult = await _writer.UpdateAsync<PollingOptions>(
                opt => opt.PollInterval = polling.PollInterval, _lifetime.Token);
            if (!pollingResult.Applied)
            {
                return new UpdateSettingsRejected(string.Join("; ", pollingResult.Failures));
            }
        }

        return new UpdateSettingsCompleted(CurrentConfig(), [.. RestartRequired(scopeResult, "")]);
    }

    // Rejects a scope or poll interval change whose projection breaks the same 80 % guard that startup validation applies.
    private string? BudgetRejection(Action<NjordOptions> applyScope, PollingOptions polling)
    {
        var scope = ConfigWriter.DeepClone(_scope.CurrentValue);
        applyScope(scope);

        try
        {
            var projection = BudgetCalculator.Validate(scope, polling);
            return BudgetCalculator.GuardViolation(projection, polling.PollInterval) is not null
                ? $"Would exceed budget: {projection.UsagePercent:F0}% of monthly limit"
                : null;
        }
        catch (ParameterResolutionException ex)
        {
            return string.Join("; ", ex.Errors);
        }
    }

    private async Task OnUpdateAnalysisConfig(UpdateAnalysisConfig cmd)
    {
        var sender = Sender;
        try
        {
            sender.Tell(await UpdateAnalysisConfigAsync(cmd));
        }
        catch (Exception ex)
        {
            sender.Tell(new UpdateAnalysisConfigFailed(ex));
        }
    }

    // Every touched section is validated before any of them is written; one write persists them all.
    private async Task<UpdateAnalysisConfigResponse> UpdateAnalysisConfigAsync(UpdateAnalysisConfig cmd)
    {
        var updates = cmd.ToAnalysisUpdates();
        if (updates.Count == 0)
        {
            return new UpdateAnalysisConfigRejected(
                [new ConfigIssue(null, "At least one section or reset is required")], cmd.DryRun);
        }

        var result = await _writer.UpdateManyAsync([.. updates.Select(u => u.Update)], cmd.DryRun, _lifetime.Token);
        if (!result.Valid)
        {
            return new UpdateAnalysisConfigRejected(
                [.. updates.Zip(result.Sections, (update, outcome) => (Update: update, Outcome: outcome)).SelectMany(pair =>
                    pair.Outcome.Failures.Select(message => new ConfigIssue(pair.Update.Section, message)))],
                cmd.DryRun);
        }

        var restartRequired = updates.Zip(result.Sections, (update, outcome) => (Update: update, Outcome: outcome)).SelectMany(pair =>
            pair.Outcome.RestartRequiredProperties.Select(p => $"{pair.Update.Prefix}{p}")).ToList();

        return cmd.DryRun
            ? new UpdateAnalysisConfigValidated(CurrentConfig(), restartRequired)
            : new UpdateAnalysisConfigCompleted(CurrentConfig(), restartRequired);
    }

    private async Task OnUpdateBudget(UpdateBudget cmd)
    {
        var sender = Sender;
        try
        {
            sender.Tell(await UpdateBudgetAsync(cmd));
        }
        catch (Exception ex)
        {
            sender.Tell(new UpdateBudgetFailed(ex));
        }
    }

    private async Task<UpdateBudgetResponse> UpdateBudgetAsync(UpdateBudget cmd)
    {
        if (cmd.RequestsPerMonth is <= 0)
        {
            return new UpdateBudgetRejected("requests_per_month must be greater than zero");
        }

        if (cmd.RequestsPerMinute is <= 0)
        {
            return new UpdateBudgetRejected("requests_per_minute must be greater than zero");
        }

        var result = await _writer.UpdateAsync<PollingOptions>(opt =>
        {
            if (cmd.RequestsPerMonth is not null || cmd.RequestsPerMinute is not null)
            {
                var current = BudgetCalculator.GetEffectiveBudget(opt);
                opt.BudgetOverride = new RequestBudget(
                    cmd.RequestsPerMonth ?? current.RequestsPerMonth,
                    cmd.RequestsPerMinute ?? current.RequestsPerMinute);
            }
            else
            {
                opt.BudgetOverride = null;
            }
        }, _lifetime.Token);

        return result.Applied
            ? new UpdateBudgetCompleted(CurrentConfig(), [])
            : new UpdateBudgetRejected(string.Join("; ", result.Failures));
    }

    private EffectiveConfig CurrentConfig() =>
        EffectiveConfigMappings.ToEffectiveConfig(
            _scope.CurrentValue,
            _polling.CurrentValue,
            _consensus.CurrentValue,
            _alerts.CurrentValue,
            _derived.CurrentValue,
            _trends.CurrentValue,
            _indices.CurrentValue);

    private static IEnumerable<string> RestartRequired<T>(ConfigUpdateResult<T> result, string section)
        where T : class =>
        result.RequiresRestart
            ? result.ChangedProperties
                .Where(p => ConfigReloadPolicy.For<T>(p) == ReloadMode.RestartRequired)
                .Select(p => $"{section}{p}")
            : [];

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
        Send<IPollManager>(next);
        _log.Info("Polling reloaded: poll interval {PollInterval}, discovery interval {DiscoveryInterval}",
            next.PollInterval, next.DiscoveryInterval);
    }

    // The gateway's budget gate, the budget tracker's status answer and the budget metrics read the budget per call.
    private void ApplyBudget(RequestBudget next)
    {
        if (next == _budget)
        {
            return;
        }

        _budget = next;
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

    private static bool Equivalent(AnalysisSettings a, AnalysisSettings b) =>
        a.Consensus == b.Consensus
        && a.Alerts == b.Alerts
        && a.Trends == b.Trends
        && a.Derived.Horizons.SequenceEqual(b.Derived.Horizons)
        && a.Indices.Preferences.Count == b.Indices.Preferences.Count
        && a.Indices.Preferences.All(p =>
            b.Indices.Preferences.TryGetValue(p.Key, out var other) && other == p.Value);
}
