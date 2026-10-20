using Akka.Actor;
using Njord.Core;

namespace Njord.Pipeline.Tests;

public sealed class PipelineSchedulerConnectionSpec : PipelineConnectionTestKit
{
    [Fact(Timeout = 30000)]
    public async Task Persistent_scheduler_connects_and_offers_through_full_pipeline()
    {
        var offered = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(
            Props.Create(() => new ProductionLikePipelineActor(offered)),
            "pipeline");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var scheduler = Sys.ActorOf(
            Props.Create(() => new ProductionLikeSchedulerActor($"sched-{Guid.NewGuid():N}")),
            "scheduler");

        var value = await offered.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
        Assert.Equal(42, value);
    }
}
