using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;
using Njord.Mqtt;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Egress;

public sealed class ModelStateActor : ReceiveActor, IWithStash
{
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ModelStateActor> _logger;

    private ISinkRef<EgressEvent>? _egressSinkRef;
    private ISourceRef<FetchOutcome>? _sourceRef;
    private IMaterializer? _mat;
    private IActorRef? _discoveryActor;

    public IStash Stash { get; set; } = null!;

    public ModelStateActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        ILogger<ModelStateActor> logger)
    {
        var opts = options.Value;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _logger = logger;

        WaitingForRefs();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        _discoveryActor = Context.GetActor<DiscoveryActor>();

        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestEgressSink());

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());
    }

    private void WaitingForRefs()
    {
        Receive<EgressSinkResponse>(response =>
        {
            _egressSinkRef = response.SinkRef;
            _logger.LogInformation("Egress SinkRef received");
            TryTransitionToReady();
        });
        Receive<PipelineSourceResponse>(response =>
        {
            _sourceRef = response.SourceRef;
            _logger.LogInformation("Pipeline SourceRef received");
            TryTransitionToReady();
        });
        Receive<Terminated>(msg => HandleTerminated(msg));
        ReceiveAny(_ => Stash.Stash());
    }

    private void TryTransitionToReady()
    {
        if (_egressSinkRef is null || _sourceRef is null) return;

        MaterializeGraph();
        _logger.LogInformation("ModelState pipeline materialized — ready");
        Become(Ready);
        Stash.UnstashAll();
    }

    private void Ready()
    {
        Receive<Terminated>(msg => HandleTerminated(msg));
    }

    private void HandleTerminated(Terminated msg)
    {
        _logger.LogWarning("Watched actor {Actor} terminated — re-requesting refs", msg.ActorRef.Path.Name);

        _egressSinkRef = null;
        _sourceRef = null;

        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestEgressSink());

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());

        Become(WaitingForRefs);
    }

    private void MaterializeGraph()
    {
        var mat = _mat!;
        var parameters = _parameters;
        var horizons = _horizons;
        var forecastDays = _forecastDays;
        var discoveryActor = _discoveryActor!;
        var logger = _logger;
        var knownCapabilities = new Dictionary<(string Location, string ModelId), HashSet<ParameterDef>>();

        _sourceRef!.Source
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .SelectMany(success =>
            {
                var forecast = success.Forecast;
                var capKey = (forecast.Location, forecast.Model.Id);
                var maxHours = ModelCoverageRegistry.Get(forecast.Model.Id)?.MaxForecastHours;

                var observedParams = ExtractSupportedParameters(forecast, parameters);

                if (!knownCapabilities.TryGetValue(capKey, out var known))
                {
                    known = new HashSet<ParameterDef>(observedParams);
                    knownCapabilities[capKey] = known;
                    SendCapabilityLearned(discoveryActor, forecast, known, horizons, forecastDays, maxHours, logger);
                }
                else if (!observedParams.IsSubsetOf(known))
                {
                    known.UnionWith(observedParams);
                    SendCapabilityLearned(discoveryActor, forecast, known, horizons, forecastDays, maxHours, logger);
                }

                return new[] { (EgressEvent)new EgressEvent.PerModelUpdate(forecast.Location, forecast.Model, forecast) };
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(_egressSinkRef!.Sink, mat);
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
                    supported.Add(param);
            }
        }

        foreach (var point in forecast.Daily.Points)
        {
            foreach (var param in parameters.Daily)
            {
                if (param.ValueType == ParameterValueType.TimeString)
                {
                    if (point.GetMeta(param) is not null)
                        supported.Add(param);
                }
                else
                {
                    if (point.GetNumeric(param) is not null)
                        supported.Add(param);
                }
            }
        }

        return supported;
    }

    private static void SendCapabilityLearned(
        IActorRef discoveryActor,
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

        var message = new ModelCapabilityLearned(
            forecast.Location,
            forecast.Model,
            supported.ToHashSet(),
            applicableHorizons,
            applicableDayOffsets);

        discoveryActor.Tell(message);
        logger.LogInformation(
            "Capability learned for {Location}/{Model}: {ParamCount} parameters, {HorizonCount} horizons",
            forecast.Location, forecast.Model.Id, supported.Count, applicableHorizons.Count);
    }
}
