using Akka;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.TestKit;
using Njord.Messages.Budget;
using Njord.OpenMeteo.Budget;

namespace Njord.OpenMeteo.Tests;

public sealed class BudgetGateStageTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private Flow<int, int, NotUsed> CreateStage(
        SwitchableBudgetProvider rate, TestProbe tracker, int maxCallWeight = 1)
    {
        return Flow.FromGraph(new BudgetGateStage<int>(
            rate, tracker.Ref, TimeProvider.System,
            _ => maxCallWeight, maxCallWeight));
    }

    [Fact(Timeout = 10000)]
    public async Task Element_passes_immediately_when_tokens_available()
    {
        var ct = TestContext.Current.CancellationToken;
        var rate = new SwitchableBudgetProvider { CostPerMinute = 6000, MaxBurst = 100 };
        var tracker = CreateTestProbe();
        var stage = CreateStage(rate, tracker, maxCallWeight: 1);

        var (queue, sink) = Source.Queue<int>(10, OverflowStrategy.DropNew)
            .Via(stage)
            .ToMaterialized(Sink.Seq<int>(), Keep.Both)
            .Run(Sys.Materializer());

        await queue.OfferAsync(42);
        queue.Complete();
        var result = await sink.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Single(result);
        Assert.Equal(42, result[0]);
        await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: ct);
    }

    [Fact(Timeout = 10000)]
    public async Task Parked_element_is_released_after_token_refill()
    {
        var ct = TestContext.Current.CancellationToken;
        var rate = new SwitchableBudgetProvider { CostPerMinute = 6000, MaxBurst = 18 };
        var tracker = CreateTestProbe();
        var stage = CreateStage(rate, tracker, maxCallWeight: 18);

        var (queue, sink) = Source.Queue<int>(10, OverflowStrategy.DropNew)
            .Via(stage)
            .ToMaterialized(Sink.Seq<int>(), Keep.Both)
            .Run(Sys.Materializer());

        await queue.OfferAsync(1);
        await queue.OfferAsync(2);
        queue.Complete();

        var result = await sink.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: ct);
        await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: ct);
    }

    [Fact(Timeout = 15000)]
    public async Task Consecutive_heavy_elements_are_serialized_not_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var rate = new SwitchableBudgetProvider { CostPerMinute = 6000, MaxBurst = 18 };
        var tracker = CreateTestProbe();
        var stage = CreateStage(rate, tracker, maxCallWeight: 18);

        var (queue, sink) = Source.Queue<int>(10, OverflowStrategy.DropNew)
            .Via(stage)
            .ToMaterialized(Sink.Seq<int>(), Keep.Both)
            .Run(Sys.Materializer());

        await queue.OfferAsync(10);
        await queue.OfferAsync(20);
        await queue.OfferAsync(30);
        queue.Complete();

        var result = await sink.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 10, 20, 30 }, result.ToArray());
    }

    [Fact(Timeout = 10000)]
    public async Task Rate_refresh_updates_the_token_rate()
    {
        var ct = TestContext.Current.CancellationToken;
        var rate = new SwitchableBudgetProvider { CostPerMinute = 6000, MaxBurst = 18 };
        var tracker = CreateTestProbe();
        var stage = CreateStage(rate, tracker, maxCallWeight: 18);

        var (queue, sink) = Source.Queue<int>(10, OverflowStrategy.DropNew)
            .Via(stage)
            .ToMaterialized(Sink.Seq<int>(), Keep.Both)
            .Run(Sys.Materializer());

        await queue.OfferAsync(1);
        await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: ct);

        rate.CostPerMinute = 60000;
        rate.MaxBurst = 18;

        await queue.OfferAsync(2);
        queue.Complete();

        var result = await sink.WaitAsync(TimeSpan.FromSeconds(8), ct);

        Assert.Equal(2, result.Count);
    }

    [Fact(Timeout = 10000)]
    public async Task Upstream_completion_drains_parked_element()
    {
        var ct = TestContext.Current.CancellationToken;
        var rate = new SwitchableBudgetProvider { CostPerMinute = 6000, MaxBurst = 18 };
        var tracker = CreateTestProbe();
        var stage = CreateStage(rate, tracker, maxCallWeight: 18);

        var (queue, sink) = Source.Queue<int>(10, OverflowStrategy.DropNew)
            .Via(stage)
            .ToMaterialized(Sink.Seq<int>(), Keep.Both)
            .Run(Sys.Materializer());

        await queue.OfferAsync(1);
        await queue.OfferAsync(2);
        queue.Complete();

        var result = await sink.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[1]);
    }
}
