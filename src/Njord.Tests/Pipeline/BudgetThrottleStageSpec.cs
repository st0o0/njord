using Akka.Persistence.TestKit;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Configuration;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class BudgetThrottleStageSpec : PersistenceTestKit
{
    private IMaterializer Mat => Sys.Materializer();

    [Fact(Timeout = 5000)]
    public async Task Elements_pass_through_when_gate_allows_immediately()
    {
        var gate = new AlwaysAllowGate<int>();
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From(Enumerable.Range(0, 5))
            .Via(stage)
            .RunWith(Sink.Seq<int>(), Mat);

        Assert.Equal(5, result.Count);
        Assert.Equal(5, gate.AcquireCount);
    }

    [Fact(Timeout = 5000)]
    public async Task Gate_is_called_for_every_element()
    {
        var gate = new AlwaysAllowGate<int>();
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From([10, 20, 30])
            .Via(stage)
            .RunWith(Sink.Seq<int>(), Mat);

        Assert.Equal([10, 20, 30], result);
        Assert.Equal([10, 20, 30], gate.Acquired);
    }

    [Fact(Timeout = 5000)]
    public async Task Stage_retries_after_delay_when_gate_rejects()
    {
        var gate = new RejectThenAllowGate<int>(rejectCount: 2);
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From([42])
            .Via(stage)
            .RunWith(Sink.Seq<int>(), Mat);

        Assert.Equal([42], result);
        Assert.Equal(3, gate.TryCount);
    }

    [Fact(Timeout = 5000)]
    public async Task Empty_source_completes_immediately()
    {
        var gate = new AlwaysAllowGate<int>();
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.Empty<int>()
            .Via(stage)
            .RunWith(Sink.Seq<int>(), Mat);

        Assert.Empty(result);
        Assert.Empty(gate.Acquired);
    }

    [Fact(Timeout = 5000)]
    public async Task Stage_preserves_element_order()
    {
        var gate = new AlwaysAllowGate<int>();
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From([1, 2, 3, 4, 5])
            .Via(stage)
            .RunWith(Sink.Seq<int>(), Mat);

        Assert.Equal([1, 2, 3, 4, 5], result);
    }

    private sealed class AlwaysAllowGate<T> : IBudgetGate<T>
    {
        public List<T> Acquired { get; } = [];
        public int AcquireCount => Acquired.Count;

        public bool TryAcquire(T element)
        {
            lock (Acquired) Acquired.Add(element);
            return true;
        }

        public TimeSpan EstimateDelay(T element) => TimeSpan.Zero;
    }

    private sealed class RejectThenAllowGate<T>(int rejectCount) : IBudgetGate<T>
    {
        public int TryCount;

        public bool TryAcquire(T element)
        {
            TryCount++;
            return TryCount > rejectCount;
        }

        public TimeSpan EstimateDelay(T element) => TimeSpan.FromMilliseconds(10);
    }
}

public sealed class WeightedBudgetGateSpec
{
    [Fact(Timeout = 5000)]
    public void Acquires_immediately_when_tokens_available()
    {
        var provider = new FakeProvider(new BudgetRate(600, 10));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        var target = MakeTarget(weight: 1);
        Assert.True(gate.TryAcquire(target));
        Assert.Equal(1, tracker.GetUsage().MonthlyUsed);
    }

    [Fact(Timeout = 5000)]
    public void Rejects_when_tokens_insufficient()
    {
        var provider = new FakeProvider(new BudgetRate(60, 1));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        Assert.True(gate.TryAcquire(MakeTarget(1)));
        Assert.False(gate.TryAcquire(MakeTarget(1)));
    }

    [Fact(Timeout = 5000)]
    public void EstimateDelay_returns_positive_when_tokens_insufficient()
    {
        var provider = new FakeProvider(new BudgetRate(60, 1));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        gate.TryAcquire(MakeTarget(1));
        var delay = gate.EstimateDelay(MakeTarget(1));

        Assert.True(delay > TimeSpan.Zero);
    }

    [Fact(Timeout = 5000)]
    public void Records_calls_with_correct_weight()
    {
        var provider = new FakeProvider(new BudgetRate(6000, 100));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        gate.TryAcquire(MakeTarget(3));
        gate.TryAcquire(MakeTarget(2));

        Assert.Equal(5, tracker.GetUsage().MonthlyUsed);
    }

    [Fact(Timeout = 5000)]
    public void Provider_is_polled_at_construction()
    {
        var provider = new FakeProvider(new BudgetRate(6000, 100));
        var tracker = new BudgetTracker();
        _ = new WeightedBudgetGate(provider, tracker);

        Assert.True(provider.CallCount >= 1);
    }

    [Fact(Timeout = 5000)]
    public void Burst_allows_multiple_immediate_acquires()
    {
        var provider = new FakeProvider(new BudgetRate(60, 4));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        for (var i = 0; i < 4; i++)
            Assert.True(gate.TryAcquire(MakeTarget(1)));

        Assert.Equal(4, tracker.GetUsage().MonthlyUsed);
        Assert.False(gate.TryAcquire(MakeTarget(1)));
    }

    [Fact(Timeout = 5000)]
    public void Weight_exceeding_max_burst_becomes_acquirable_after_refill()
    {
        var provider = new FakeProvider(new BudgetRate(480, 16));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        gate.TryAcquire(MakeTarget(16));
        Assert.False(gate.TryAcquire(MakeTarget(12)));

        Thread.Sleep(1600);
        Assert.True(gate.TryAcquire(MakeTarget(12)));
    }

    [Fact(Timeout = 5000)]
    public void Free_tier_with_heavy_weight_acquires_on_first_try()
    {
        var provider = new FakeProvider(new BudgetRate(480, 16));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        var heavyTarget = MakeTarget(weight: 12);
        Assert.True(gate.TryAcquire(heavyTarget));
    }

    private static WeightedTarget MakeTarget(int weight)
    {
        var location = new LocationOptions
        {
            Name = "test",
            Latitude = 0,
            Longitude = 0,
        };
        return new WeightedTarget(location, new Njord.Domain.Weather.WeatherModel("test"),
            weight, new Njord.Domain.Weather.CycleId(DateTimeOffset.UtcNow));
    }

    private sealed class FakeProvider(BudgetRate rate) : IBudgetProvider
    {
        public BudgetRate Rate { get; set; } = rate;
        public int CallCount;
        public BudgetRate GetCurrentRate() { Interlocked.Increment(ref CallCount); return Rate; }
    }
}
