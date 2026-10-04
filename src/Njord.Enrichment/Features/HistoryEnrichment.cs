using System.Diagnostics.Metrics;
using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Analysis;
using Njord.Configuration;
using Njord.Diagnostics;
using Njord.Domain.Weather;
using Njord.Messages.Egress;
using Njord.Messages.Enrichment;

namespace Njord.Enrichment.Features;

internal sealed class HistoryEnrichment : IActorEnrichment
{
    private static readonly Gauge<double> HistoryMae = NjordMetrics.Instance.AddHistoryMae();
    private static readonly Gauge<double> HistoryModelWeight = NjordMetrics.Instance.AddHistoryModelWeight();
    private readonly NjordOptions _njordOptions;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly HistoryComputer _computer;
    private readonly HistoryOptions _historyOptions;
    private readonly ILogger<HistoryEnrichment> _logger;
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.History;
    public bool Enabled => _enabled;

    public HistoryEnrichment(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        HistoryComputer computer,
        ILogger<HistoryEnrichment> logger)
    {
        _njordOptions = options.Value;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _computer = computer;
        _historyOptions = options.Value.Enrichment.History;
        _logger = logger;
        _enabled = options.Value.Enrichment.IsEnabled(TypeName);
    }

    public Flow<ModelSnapshot, EgressEvent, NotUsed> CreateFlow(IUntypedActorContext context)
    {
        var locations = _njordOptions.Locations.Select(l => l.Name).ToList();
        var computer = _computer;
        var parameters = _parameters;
        var timeProvider = _timeProvider;
        var historyOptions = _historyOptions;

        var historyRegion = ActorRegistry.For(context.System).Get<IForecastHistoryRegion>();

        return Flow.Create<ModelSnapshot>()
            .SelectAsync(1, async snapshot =>
            {
                foreach (var location in locations)
                {
                    historyRegion.Tell(new RecordSnapshot(location, snapshot));
                }

                var events = new List<EgressEvent>();
                foreach (var location in locations)
                {
                    var response = await historyRegion.Ask<QueryHistoryResult>(
                        new QueryHistory(location), TimeSpan.FromSeconds(5));
                    var result = computer.Compute(
                        response.History, snapshot, location, parameters, timeProvider, historyOptions);
                    RecordHistoryMetrics(result);
                    events.Add(new EgressEvent.EnrichmentUpdate(location, TypeName, result));
                }
                return events;
            })
            .SelectMany(events => events)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_logger)));
    }

    private static void RecordHistoryMetrics(HistoryResult result)
    {
        var locationTag = new KeyValuePair<string, object?>("location", result.Location);
        foreach (var (model, mae) in result.Mae7d)
        {
            if (mae.HasValue)
            {
                HistoryMae.Record(mae.Value, locationTag,
                    new KeyValuePair<string, object?>("model", model.Id));
            }
        }
        foreach (var (model, weight) in result.Weights)
        {
            HistoryModelWeight.Record(weight, locationTag,
                new KeyValuePair<string, object?>("model", model.Id));
        }
    }
}
