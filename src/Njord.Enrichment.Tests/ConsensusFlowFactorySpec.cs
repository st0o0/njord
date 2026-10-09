using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Compute.Analysis;
using Njord.Messages.Egress;
using Njord.Messages.Sensors;
using Njord.Sensors;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Enrichment.Tests;

public sealed class ConsensusFlowFactorySpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static readonly DateTimeOffset T0 = TestTimeConstants.EnrichmentEpoch;
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private sealed class RecordingEnrichment : IStatelessEnrichment
    {
        public List<SensorSnapshot?> ReceivedSensors { get; } = [];
        public string TypeName => "stub";
        public bool Enabled => true;

        public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null)
        {
            ReceivedSensors.Add(sensors);
            yield return new EgressEvent.EnrichmentUpdate(
                consensus.Location, TypeName,
                new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters, consensus.ComputedAt),
                consensus.ComputedAt);
        }
    }

    private sealed class RecordingStateful : IStatefulEnrichment
    {
        public List<ConsensusSnapshot?> ReceivedPrevious { get; } = [];
        public string TypeName => "stateful-stub";
        public bool Enabled => true;

        public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous)
        {
            ReceivedPrevious.Add(previous);
            yield return new EgressEvent.EnrichmentUpdate(
                consensus.Location, TypeName,
                new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters, consensus.ComputedAt),
                consensus.ComputedAt);
        }
    }

    private sealed class FakeSensorHub : ReceiveActor
    {
        public FakeSensorHub(SensorSnapshot? snapshot)
        {
            if (snapshot is not null)
            {
                Receive<QuerySensorSnapshot>(_ => Sender.Tell(new QuerySensorSnapshotCompleted(snapshot)));
            }
            else
            {
                Receive<QuerySensorSnapshot>(_ => { });
            }
        }
    }

    private static ConsensusSnapshot Consensus(ModelSnapshot snapshot) =>
        new ConsensusSnapshotFactory(Parameters, TestTimeouts.AtEpoch(T0)).Create(snapshot, "lucerne");

    private static SensorSnapshot Sensors() =>
        new("lucerne", new Dictionary<SensorKind, AggregatedReading>
        {
            [SensorKind.IndoorTemperature] = new(21.5, 1, T0),
        });

    private async Task<IReadOnlyList<EgressEvent>> Run(
        CancellationToken ct,
        IActorRef sensorHub,
        TimeSpan timeout,
        bool consensusEnabled,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful,
        params ConsensusSnapshot[] inputs)
    {
        var flow = ConsensusFlowFactory.Create(sensorHub, timeout, consensusEnabled, stateless, stateful, Sys.Log);
        return await Source.From(inputs)
            .Via(flow)
            .RunWith(Sink.Seq<EgressEvent>(), Sys.Materializer())
            .WaitAsync(ct);
    }

    [Fact(Timeout = 10000)]
    public async Task Sensor_snapshot_is_passed_to_stateless_features()
    {
        var hub = Sys.ActorOf(Props.Create(() => new FakeSensorHub(Sensors())));
        var feature = new RecordingEnrichment();

        var events = await Run(TestContext.Current.CancellationToken, hub, TimeSpan.FromSeconds(1), false, [feature], [],
            Consensus(ModelSnapshot.Empty));

        var evt = Assert.Single(events);
        Assert.Equal("stub", Assert.IsType<EgressEvent.EnrichmentUpdate>(evt).TypeName);
        var received = Assert.Single(feature.ReceivedSensors);
        Assert.NotNull(received);
        Assert.Equal(21.5, received.Readings[SensorKind.IndoorTemperature].Value);
    }

    [Fact(Timeout = 10000)]
    public async Task Sensor_timeout_proceeds_without_sensor_data()
    {
        var hub = Sys.ActorOf(Props.Create(() => new FakeSensorHub(null)));
        var feature = new RecordingEnrichment();

        var events = await Run(TestContext.Current.CancellationToken, hub, TimeSpan.FromMilliseconds(100), false, [feature], [],
            Consensus(ModelSnapshot.Empty));

        Assert.Single(events);
        Assert.Null(Assert.Single(feature.ReceivedSensors));
    }

    [Fact(Timeout = 10000)]
    public async Task Empty_snapshot_with_consensus_enabled_emits_consensus_update_first()
    {
        var hub = Sys.ActorOf(Props.Create(() => new FakeSensorHub(null)));
        var feature = new RecordingEnrichment();

        var events = await Run(TestContext.Current.CancellationToken, hub, TimeSpan.FromMilliseconds(100), true, [feature], [],
            Consensus(ModelSnapshot.Empty));

        Assert.Equal(2, events.Count);
        Assert.Equal("consensus", Assert.IsType<EgressEvent.EnrichmentUpdate>(events[0]).TypeName);
        Assert.Equal("stub", Assert.IsType<EgressEvent.EnrichmentUpdate>(events[1]).TypeName);
    }

    [Fact(Timeout = 10000)]
    public async Task Stateful_features_receive_previous_consensus()
    {
        var hub = Sys.ActorOf(Props.Create(() => new FakeSensorHub(null)));
        var feature = new RecordingStateful();
        var first = Consensus(ModelSnapshot.Empty);
        var second = Consensus(ModelSnapshot.Empty);

        var events = await Run(TestContext.Current.CancellationToken, hub, TimeSpan.FromMilliseconds(100), false, [], [feature], first, second);

        Assert.Equal(2, events.Count);
        Assert.Equal(2, feature.ReceivedPrevious.Count);
        Assert.Null(feature.ReceivedPrevious[0]);
        Assert.Same(first, feature.ReceivedPrevious[1]);
    }
}
