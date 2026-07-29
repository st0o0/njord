using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Egress;

public sealed class ModelStateActor : StreamConsumerActor
{
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly ResolvedParameterSet _parameters;
    private readonly ILogger<ModelStateActor> _logger;

    private ISinkRef<EgressEvent>? _egressSinkRef;
    private ISourceRef<FetchOutcome>? _sourceRef;

    private sealed record EgressResolved(IActorRef Ref);
    private sealed record PipelineResolved(IActorRef Ref);

    public ModelStateActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        ILogger<ModelStateActor> logger)
    {
        var opts = options.Value;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _parameters = parameters;
        _logger = logger;
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
        Context.GetActorAsync<PipelineActor>().PipeTo(Self, success: r => new PipelineResolved(r));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<EgressResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestEgressSink());
        });
        Receive<PipelineResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestPipelineSource());
        });
        Receive<EgressSinkResponse>(response =>
        {
            _egressSinkRef = response.SinkRef;
            _logger.LogInformation("Egress SinkRef received");
            TryTransition();
        });
        Receive<PipelineSourceResponse>(response =>
        {
            _sourceRef = response.SourceRef;
            _logger.LogInformation("Pipeline SourceRef received");
            TryTransition();
        });
    }

    protected override bool AllRefsReady() => _egressSinkRef is not null && _sourceRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var parameters = _parameters;
        var horizons = _horizons;
        var forecastDays = _forecastDays;
        var logger = _logger;
        var knownCapabilities = new Dictionary<(string Location, string ModelId), HashSet<ParameterDef>>();

        _sourceRef!.Source
            .Via(killSwitch.Flow<FetchOutcome>())
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .SelectMany(success =>
            {
                var forecast = success.Forecast;
                var capKey = (forecast.Location, forecast.Model.Id);
                var maxHours = ModelCoverageRegistry.Get(forecast.Model.Id)?.MaxForecastHours;

                var observedParams = ExtractSupportedParameters(forecast, parameters);
                var events = new List<EgressEvent>();

                if (!knownCapabilities.TryGetValue(capKey, out var known))
                {
                    known = new HashSet<ParameterDef>(observedParams);
                    knownCapabilities[capKey] = known;
                    events.Add(BuildCapabilityLearned(forecast, known, horizons, forecastDays, maxHours, logger));
                }
                else if (!observedParams.IsSubsetOf(known))
                {
                    known.UnionWith(observedParams);
                    events.Add(BuildCapabilityLearned(forecast, known, horizons, forecastDays, maxHours, logger));
                }

                events.Add(new EgressEvent.PerModelUpdate(forecast.Location, forecast.Model, forecast));
                return events;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_logger)))
            .RunWith(_egressSinkRef!.Sink, Mat);
    }

    protected override void OnDependencyLost()
    {
        _egressSinkRef = null;
        _sourceRef = null;
    }

    private static HashSet<ParameterDef> ExtractSupportedParameters(
        ModelForecast forecast, ResolvedParameterSet parameters)
    {
        var supported = new HashSet<ParameterDef>();

        foreach (var point in forecast.Hourly.Points)
        {
            foreach (var param in parameters.Hourly)
            {
                if (point.Get(param) is not null)
                {
                    supported.Add(param);
                }
            }
        }

        foreach (var point in forecast.Daily.Points)
        {
            foreach (var param in parameters.Daily)
            {
                if (param.ValueType == ParameterValueType.TimeString)
                {
                    if (point.GetMeta(param) is not null)
                    {
                        supported.Add(param);
                    }
                }
                else
                {
                    if (point.GetNumeric(param) is not null)
                    {
                        supported.Add(param);
                    }
                }
            }
        }

        return supported;
    }

    private static EgressEvent.CapabilityLearned BuildCapabilityLearned(
        ModelForecast forecast,
        HashSet<ParameterDef> supported,
        IReadOnlyList<int> horizons,
        int forecastDays,
        int? maxForecastHours,
        ILogger logger)
    {
        var applicableHorizons = maxForecastHours.HasValue
            ? horizons.Where(h => h <= maxForecastHours.Value).ToList()
            : horizons.ToList();

        var maxDays = maxForecastHours.HasValue
            ? (int)Math.Ceiling(maxForecastHours.Value / 24.0)
            : forecastDays;
        var applicableDayOffsets = Enumerable.Range(0, Math.Min(forecastDays, maxDays)).ToList();

        logger.LogInformation(
            "Capability learned for {Location}/{Model}: {ParamCount} parameters, {HorizonCount} horizons",
            forecast.Location, forecast.Model.Id, supported.Count, applicableHorizons.Count);

        return new EgressEvent.CapabilityLearned(
            forecast.Location,
            forecast.Model,
            supported.ToHashSet(),
            applicableHorizons,
            applicableDayOffsets);
    }
}
