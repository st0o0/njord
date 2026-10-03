using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Egress;
using Njord.Messages.Common;
using Njord.Messages.Egress;
using Njord.Tests.Shared;

namespace Njord.Egress.Tests;

public sealed class EgressActorShutdownSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stop_streams_completes_graph_without_abrupt_termination()
    {
        var egress = Sys.ActorOf(Props.Create<EgressActor>());

        await EventFilter.Exception<AbruptTerminationException>().ExpectAsync(
            0,
            TimeSpan.FromSeconds(1),
            async () =>
            {
                var reply = await egress.Ask<object>(new StopStreams(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.IsType<StreamsStopped>(reply);

                await Sys.Terminate();
            },
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stop_streams_completes_connected_source_ref_consumers()
    {
        var egress = Sys.ActorOf(Props.Create<EgressActor>());
        var source = await egress.Ask<EgressSourceResponse>(
            new RequestEgressSource(0), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var consumer = source.SourceRef.Source.RunWith(Sink.Ignore<EgressEvent>(), Sys.Materializer());

        var reply = await egress.Ask<object>(new StopStreams(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.IsType<StreamsStopped>(reply);
        await consumer.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
