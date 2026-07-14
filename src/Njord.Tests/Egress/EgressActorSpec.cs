using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class EgressActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("egress-spec");

    public void Dispose() => _system.Dispose();

    [Fact(Timeout = 5000)]
    public async Task Vends_sink_ref_on_request()
    {
        var egress = _system.ActorOf(Props.Create<EgressActor>());

        var response = await egress.Ask<EgressSinkResponse>(new RequestEgressSink(), TimeSpan.FromSeconds(2));

        Assert.NotNull(response.SinkRef);
    }

    [Fact(Timeout = 5000)]
    public async Task Vends_source_ref_on_request()
    {
        var egress = _system.ActorOf(Props.Create<EgressActor>());

        var response = await egress.Ask<EgressSourceResponse>(new RequestEgressSource(), TimeSpan.FromSeconds(2));

        Assert.NotNull(response.SourceRef);
    }

    [Fact(Timeout = 5000)]
    public async Task Events_flow_from_producer_to_consumer_through_hub()
    {
        var mat = _system.Materializer();
        var egress = _system.ActorOf(Props.Create<EgressActor>());

        var sinkResponse = await egress.Ask<EgressSinkResponse>(new RequestEgressSink(), TimeSpan.FromSeconds(2));
        var sourceResponse = await egress.Ask<EgressSourceResponse>(new RequestEgressSource(), TimeSpan.FromSeconds(2));

        var received = new List<EgressEvent>();
        var completionSource = new TaskCompletionSource();
        _ = sourceResponse.SourceRef.Source
            .Take(1)
            .RunForeach(e => received.Add(e), mat)
            .ContinueWith(_ => completionSource.TrySetResult());

        var testEvent = new EgressEvent.AlertUpdate("lucerne", new AlertResult("lucerne", []));

        Source.Single((EgressEvent)testEvent)
            .RunWith(sinkResponse.SinkRef.Sink, mat);

        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Single(received);
        Assert.IsType<EgressEvent.AlertUpdate>(received[0]);
        Assert.Equal("lucerne", ((EgressEvent.AlertUpdate)received[0]).Location);
    }

    [Fact(Timeout = 5000)]
    public void All_egress_event_variants_are_pattern_matchable()
    {
        var events = new EgressEvent[]
        {
            new EgressEvent.PerModelUpdate("loc", new WeatherModel("icon_d2"), new Dictionary<string, string>()),
            new EgressEvent.ConsensusUpdate("loc", new ConsensusResult([])),
            new EgressEvent.AlertUpdate("loc", new AlertResult("loc", [])),
            new EgressEvent.DerivedUpdate("loc", new DerivedResult("loc", new Dictionary<string, HorizonDerived>(), new ScalarDerived(null, null, null))),
            new EgressEvent.TrendUpdate("loc", new TrendResult("loc", new Dictionary<string, ParameterTrend?>(), null, default, default, null, null)),
            new EgressEvent.IndexUpdate("loc", new IndexResult("loc", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null)),
            new EgressEvent.EnergyUpdate("loc", new EnergyResult("loc", 0, null, [], 0, "hold", 0)),
            new EgressEvent.HistoryUpdate("loc", new HistoryResult("loc", [], [], [], [], null, null, null)),
        };

        foreach (var e in events)
        {
            var matched = e switch
            {
                EgressEvent.PerModelUpdate => "per-model",
                EgressEvent.ConsensusUpdate => "consensus",
                EgressEvent.AlertUpdate => "alert",
                EgressEvent.DerivedUpdate => "derived",
                EgressEvent.TrendUpdate => "trend",
                EgressEvent.IndexUpdate => "index",
                EgressEvent.EnergyUpdate => "energy",
                EgressEvent.HistoryUpdate => "history",
                _ => throw new InvalidOperationException("Unknown variant"),
            };

            Assert.NotNull(matched);
        }
    }
}
