using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Messages.Forecast;

namespace Njord.OpenMeteo.Tests;

internal sealed class ForwardingActor : ReceiveActor
{
    public ForwardingActor(IActorRef target)
    {
        ReceiveAny(target.Tell);
    }
}

// Vends a SinkRef per request (or never answers) whose forecasts go to the probe, and reports every request it received.
internal sealed class FakeForecastPipeline : ReceiveActor
{
    public FakeForecastPipeline(IActorRef attached, IActorRef forecasts, bool answers = true)
    {
        var mat = Context.Materializer();
        ReceiveAsync<RequestForecastSink>(async msg =>
        {
            if (answers)
            {
                var requester = Sender;
                var sinkRef = await StreamRefs.SinkRef<DomainDataReceived>()
                    .To(Sink.ActorRef<DomainDataReceived>(forecasts, "completed", _ => "failed"))
                    .Run(mat);
                requester.Tell(new RequestForecastSinkCompleted(msg.RequestId, sinkRef), Self);
            }

            attached.Tell("requested");
        });
    }
}

internal sealed class SwitchableBudgetProvider : IBudgetProvider
{
    public volatile int MaxBurst = 1_000_000;

    public volatile int CostPerMinute = 480;

    public BudgetRate GetCurrentRate() => new(CostPerMinute, MaxBurst);
}
