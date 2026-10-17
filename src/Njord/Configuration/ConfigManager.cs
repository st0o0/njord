using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
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
using Njord.Messages.Config;
using Njord.Weather;

namespace Njord.Configuration;

// The only subscriber to configuration change notifications and the only writer of runtime configuration.
// A reload is turned into typed messages for the actors that consume hot settings and a new snapshot on the
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
    private readonly NjordHealthMonitor _health;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly List<IDisposable> _subscriptions = [];
    private readonly CancellationTokenSource _lifetime = new();

    private AnalysisSettings _analysis;
    private PollingChanged _pollingState;
    private RequestBudget _budget;
    private IMaterializer? _mat;
    private ISourceQueueWithComplete<ConfigSnapshot>? _streamQueue;
    private Source<ConfigSnapshot, NotUsed>? _streamSource;

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
        IConfigWriter writer,
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
        _writer = writer;
        _health = health;

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
        var (queue, source) = Source.Queue<ConfigSnapshot>(StreamBufferSize, OverflowStrategy.DropHead)
            .ToMaterialized(BroadcastHub.Sink<ConfigSnapshot>(StreamBufferSize), Keep.Both)
            .Run(_mat);
        source.RunWith(Sink.Ignore<ConfigSnapshot>(), _mat);
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
            ApplyPolling(ToMessage(_polling.CurrentValue));
            ApplyBudget(BudgetCalculator.GetEffectiveBudget(_polling.CurrentValue));
            _streamQueue?.OfferAsync(CurrentSnapshot());
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
            Sender.Tell(new ConfigResult(CurrentSnapshot()));
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

    // Each subscriber starts with the current snapshot; later snapshots come from the hub, which the
    // subscriber joins here, in the same turn, so no reload between the two is lost.
    private void OnRequestConfigStream(RequestConfigStream msg)
    {
        ConfigSnapshot current;
        try
        {
            current = CurrentSnapshot();
        }
        catch (Exception ex)
        {
            Sender.Tell(new RequestConfigStreamFailed(msg.RequestId, ex));
            return;
        }

        Source.Single(current)
            .Concat(_streamSource!)
            .RunWith(StreamRefs.SourceRef<ConfigSnapshot>(), _mat!)
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
            ? new UpdateLocationsCompleted(CurrentSnapshot(), [.. RestartRequired(result, "")])
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

        if (BudgetRejection(scope => ApplyScope(scope, cmd), polling) is { } budgetRejection)
        {
            return new UpdateSettingsRejected(budgetRejection);
        }

        var failures = _writer.Check<NjordOptions>(opt => ApplyScope(opt, cmd));
        if (failures.Count > 0)
        {
            return new UpdateSettingsRejected(string.Join("; ", failures));
        }

        var scopeResult = await _writer.UpdateAsync<NjordOptions>(opt => ApplyScope(opt, cmd), _lifetime.Token);
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

        return new UpdateSettingsCompleted(CurrentSnapshot(), [.. RestartRequired(scopeResult, "")]);
    }

    private static void ApplyScope(NjordOptions opt, UpdateSettings cmd)
    {
        if (cmd.ForecastDays is { } days)
        {
            opt.ForecastDays = days;
        }

        if (cmd.Horizons.Count > 0)
        {
            opt.Horizons = [.. cmd.Horizons];
        }

        if (cmd.DefaultModels.Count > 0)
        {
            opt.Models = [.. cmd.DefaultModels];
        }

        if (cmd.Parameters is { } parameters)
        {
            opt.Parameters = new ParameterOptions
            {
                Groups = [.. parameters.Groups],
                Extra = [.. parameters.Extra],
                Exclude = [.. parameters.Exclude],
            };
        }
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
        var reset = cmd.Reset ?? [];
        var updates = BuildAnalysisUpdates(cmd, reset);
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
            ? new UpdateAnalysisConfigValidated(CurrentSnapshot(), restartRequired)
            : new UpdateAnalysisConfigCompleted(CurrentSnapshot(), restartRequired);
    }

    private sealed record AnalysisUpdate(EnrichmentSection Section, string Prefix, SectionUpdate Update);

    private static List<AnalysisUpdate> BuildAnalysisUpdates(UpdateAnalysisConfig cmd, IReadOnlyList<EnrichmentSection> reset)
    {
        var updates = new List<AnalysisUpdate>();
        Add(EnrichmentSection.Consensus, cmd.Consensus is not null,
            s => SectionUpdate.For<ConsensusOptions>(opt => { if (cmd.Consensus is { } c) ApplyConsensus(opt, c); }, s));
        Add(EnrichmentSection.Alerts, cmd.Alerts is not null,
            s => SectionUpdate.For<AlertOptions>(opt => { if (cmd.Alerts is { } a) ApplyAlerts(opt, a); }, s));
        Add(EnrichmentSection.Derived, cmd.DerivedEnabled is not null,
            s => SectionUpdate.For<DerivedOptions>(opt => { if (cmd.DerivedEnabled is { } d) opt.Enabled = d; }, s));
        Add(EnrichmentSection.Trends, cmd.TrendsEnabled is not null,
            s => SectionUpdate.For<TrendOptions>(opt => { if (cmd.TrendsEnabled is { } t) opt.Enabled = t; }, s));
        Add(EnrichmentSection.Indices, cmd.Indices is not null,
            s => SectionUpdate.For<IndexOptions>(opt => { if (cmd.Indices is { } i) ApplyIndices(opt, i); }, s));
        return updates;

        void Add(EnrichmentSection section, bool hasChange, Func<bool, SectionUpdate> create)
        {
            var isReset = reset.Contains(section);
            if (hasChange || isReset)
            {
                updates.Add(new AnalysisUpdate(section, $"Enrichment.{section}.", create(isReset)));
            }
        }
    }

    private static void ApplyConsensus(ConsensusOptions opt, ConsensusConfigUpdate update)
    {
        if (update.Enabled is { } enabled) opt.Enabled = enabled;
        if (update.Method is { } method) opt.Method = method;
        if (update.TrimPercent is { } trimPercent) opt.TrimPercent = trimPercent;
    }

    private static void ApplyAlerts(AlertOptions opt, AlertConfigUpdate update)
    {
        if (update.Enabled is { } enabled) opt.Enabled = enabled;
        if (update.FrostThresholds is { } frost) opt.FrostThresholds = [.. frost];
        if (update.HeatThresholds is { } heat) opt.HeatThresholds = [.. heat];
        if (update.StormGustThresholds is { } storm) opt.StormGustThresholds = [.. storm];
        if (update.HeavyRainHourlyThreshold is { } rainHourly) opt.HeavyRainHourlyThreshold = rainHourly;
        if (update.HeavyRainDailyThreshold is { } rainDaily) opt.HeavyRainDailyThreshold = rainDaily;
        if (update.PressureDropThreshold is { } pressureDrop) opt.PressureDropThreshold = pressureDrop;
        if (update.CapeThreshold is { } cape) opt.CapeThreshold = cape;
        if (update.ThunderstormPrecipThreshold is { } stormPrecip) opt.ThunderstormPrecipThreshold = stormPrecip;
        if (update.ThunderstormGustThreshold is { } stormGust) opt.ThunderstormGustThreshold = stormGust;
        if (update.PressureDropSevereThreshold is { } pressureSevere) opt.PressureDropSevereThreshold = pressureSevere;
        if (update.FogPersistentHours is { } fog) opt.FogPersistentHours = fog;
        if (update.IceThreshold is { } ice) opt.IceThreshold = ice;
        if (update.WindChillThresholds is { } windChill) opt.WindChillThresholds = [.. windChill];
        if (update.VisibilityThresholds is { } visibility) opt.VisibilityThresholds = [.. visibility];
        if (update.TropicalNightThresholds is { } tropical) opt.TropicalNightThresholds = [.. tropical];
        if (update.HumidityThresholds is { } humidity) opt.HumidityThresholds = [.. humidity];
    }

    private static void ApplyIndices(IndexOptions opt, IndexConfigUpdate update)
    {
        if (update.Enabled is { } enabled) opt.Enabled = enabled;
        if (update.IndoorTemp is { } indoor) opt.Preferences.IndoorTemp = indoor;
        if (update.IdealOutdoorTemp is { } outdoor) opt.Preferences.IdealOutdoorTemp = outdoor;
        if (update.HeatSensitivity is { } heat) opt.Preferences.HeatSensitivity = heat;
        if (update.HumiditySensitivity is { } humidity) opt.Preferences.HumiditySensitivity = humidity;
        if (update.WindSensitivity is { } wind) opt.Preferences.WindSensitivity = wind;
        if (update.RainSensitivity is { } rain) opt.Preferences.RainSensitivity = rain;
        if (update.RunningIdealTempLow is { } runningLow) opt.Preferences.RunningIdealTempLow = runningLow;
        if (update.RunningIdealTempHigh is { } runningHigh) opt.Preferences.RunningIdealTempHigh = runningHigh;
        if (update.BbqMinTemp is { } bbqMin) opt.Preferences.BbqMinTemp = bbqMin;
        if (update.BbqIdealWindLow is { } bbqWindLow) opt.Preferences.BbqIdealWindLow = bbqWindLow;
        if (update.BbqIdealWindHigh is { } bbqWindHigh) opt.Preferences.BbqIdealWindHigh = bbqWindHigh;
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
            ? new UpdateBudgetCompleted(CurrentSnapshot(), [])
            : new UpdateBudgetRejected(string.Join("; ", result.Failures));
    }

    private ConfigSnapshot CurrentSnapshot() =>
        ConfigSnapshotMapping.ToSnapshot(
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

    // The gateway's budget gate reads the budget per call; the health snapshot behind the budget metrics
    // (and the budget tracker's status answer) is refreshed here so the reported limit matches.
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
