using System.Diagnostics;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Messages.Egress;
using Njord.Messages.Sensors;
using Njord.Sensors;

namespace Njord.Enrichment;

public static class ConsensusFlowFactory
{
    public static Flow<ConsensusSnapshot, EgressEvent, NotUsed> Create(
        IActorRef sensorHub,
        TimeSpan sensorTimeout,
        bool consensusEgressEnabled,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful,
        ILoggingAdapter log)
    {
        return Flow.Create<ConsensusSnapshot>()
            .Scan(
                (Previous: (ConsensusSnapshot?)null, Current: (ConsensusSnapshot?)null),
                (state, consensus) => (Previous: state.Current, Current: consensus))
            .Skip(1)
            .SelectAsync(1, async pair =>
            {
                var consensus = pair.Current!;
                SensorSnapshot? sensors = null;
                try
                {
                    var response = await sensorHub.Ask<QuerySensorSnapshotResponse>(
                        new QuerySensorSnapshot(consensus.Location), sensorTimeout);
                    if (response is QuerySensorSnapshotCompleted found)
                    {
                        sensors = found.Snapshot;
                    }
                }
                catch (AskTimeoutException)
                {
                    log.Warning("SensorHub Ask timed out for {Location}, proceeding without sensor data", consensus.Location);
                }

                var sw = Stopwatch.StartNew();
                var events = ComputeAll(consensus, pair.Previous, sensors, consensusEgressEnabled, stateless, stateful).ToList();
                sw.Stop();
                if (events.Count > 0)
                {
                    var locationTag = new KeyValuePair<string, object?>("location", consensus.Location);
                    var featureNames = events
                        .Select(e => e is EgressEvent.EnrichmentUpdate eu ? eu.TypeName : null)
                        .Where(t => t is not null)
                        .Distinct()
                        .ToList();
                    foreach (var feature in featureNames)
                    {
                        Telemetry.EnrichmentDuration.Record(sw.Elapsed.TotalSeconds, locationTag,
                            new KeyValuePair<string, object?>("feature", feature));
                    }
                    log.Debug("Enrichment computed for {Location}: {Features}", consensus.Location,
                        string.Join(", ", featureNames));
                }

                RecordConsensusQuality(consensus);
                return events;
            })
            .SelectMany(events => events)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(log)));
    }

    private static void RecordConsensusQuality(ConsensusSnapshot consensus)
    {
        var locationTag = new KeyValuePair<string, object?>("location", consensus.Location);
        var tempParam = consensus.Hourly.Parameters
            .FirstOrDefault(p => p.Parameter.ApiName == "temperature_2m");
        if (tempParam is null)
        {
            return;
        }

        var firstHorizon = tempParam.ByHorizon.Values.FirstOrDefault();
        if (firstHorizon is null)
        {
            return;
        }

        Telemetry.ConsensusModels.Record(firstHorizon.AvailableModels.Count, locationTag);
        if (firstHorizon.Spread.HasValue)
        {
            Telemetry.ConsensusSpread.Record(firstHorizon.Spread.Value, locationTag);
        }
    }

    private static IEnumerable<EgressEvent> ComputeAll(
        ConsensusSnapshot consensus,
        ConsensusSnapshot? previous,
        SensorSnapshot? sensors,
        bool consensusEgressEnabled,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful)
    {
        if (consensusEgressEnabled)
        {
            var result = new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters, consensus.ComputedAt);
            yield return new EgressEvent.EnrichmentUpdate(consensus.Location, "consensus", result, consensus.ComputedAt);
        }

        foreach (var feature in stateless)
        {
            foreach (var evt in feature.Compute(consensus, sensors))
            {
                yield return evt;
            }
        }

        foreach (var feature in stateful)
        {
            foreach (var evt in feature.Compute(consensus, previous))
            {
                yield return evt;
            }
        }
    }
}
