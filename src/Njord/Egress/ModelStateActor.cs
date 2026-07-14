using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;
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
        var timeProvider = _timeProvider;
        var lastPublishedHorizon = new Dictionary<(string Location, string ModelId, string Horizon), string>();

        _sourceRef!.Source
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .SelectMany(success =>
            {
                var forecast = success.Forecast;
                var perHorizon = HorizonProjection.BuildPerHorizon(
                    forecast, parameters, horizons, forecastDays, timeProvider.GetUtcNow());

                var changed = new Dictionary<string, string>();
                foreach (var (horizon, payload) in perHorizon)
                {
                    var key = (forecast.Location, forecast.Model.Id, horizon);
                    if (lastPublishedHorizon.TryGetValue(key, out var cached) && cached == payload)
                        continue;

                    lastPublishedHorizon[key] = payload;
                    changed[horizon] = payload;
                }

                if (changed.Count == 0)
                    return [];

                return new[] { (EgressEvent)new EgressEvent.PerModelUpdate(forecast.Location, forecast.Model, changed) };
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(_egressSinkRef!.Sink, mat);
    }
}
