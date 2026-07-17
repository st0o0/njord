using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Configuration;
using Njord.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Tests.Pipeline;

public sealed class BudgetThrottleStageSpec : IAsyncLifetime
{
    private ActorSystem _system = null!;
    private IMaterializer _mat = null!;

    public ValueTask InitializeAsync()
    {
        _system = ActorSystem.Create("throttle-spec-" + Guid.NewGuid().ToString("N")[..8]);
        _mat = _system.Materializer();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _system.Terminate();
    }

    [Fact(Timeout = 5000)]
    public async Task Elements_pass_through_when_gate_allows_immediately()
    {
        var gate = new RecordingGate<int>();
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From(Enumerable.Range(0, 5))
            .Via(stage)
            .RunWith(Sink.Seq<int>(), _mat);

        Assert.Equal(5, result.Count);
        Assert.Equal(5, gate.Acquired.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Gate_is_called_for_every_element()
    {
        var gate = new RecordingGate<int>();
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From([10, 20, 30])
            .Via(stage)
            .RunWith(Sink.Seq<int>(), _mat);

        Assert.Equal([10, 20, 30], result);
        Assert.Equal([10, 20, 30], gate.Acquired);
    }

    [Fact(Timeout = 5000)]
    public async Task Stage_completes_after_pending_element()
    {
        var gate = new RecordingGate<int>(delayMs: 50);
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From([1, 2, 3])
            .Via(stage)
            .RunWith(Sink.Seq<int>(), _mat);

        Assert.Equal(3, result.Count);
        Assert.Equal([1, 2, 3], result);
        Assert.Equal(3, gate.Acquired.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Empty_source_completes_immediately()
    {
        var gate = new RecordingGate<int>();
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.Empty<int>()
            .Via(stage)
            .RunWith(Sink.Seq<int>(), _mat);

        Assert.Empty(result);
        Assert.Empty(gate.Acquired);
    }

    private sealed class RecordingGate<T>(int delayMs = 0) : IBudgetGate<T>
    {
        public List<T> Acquired { get; } = [];

        public async Task AcquireAsync(T element, CancellationToken ct = default)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs, ct);
            lock (Acquired)
                Acquired.Add(element);
        }
    }
}

public sealed class WeightedBudgetGateSpec
{
    [Fact(Timeout = 5000)]
    public async Task Acquires_immediately_when_tokens_available()
    {
        var provider = new FakeProvider(new BudgetRate(600, 10));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        var target = MakeTarget(weight: 1);
        await gate.AcquireAsync(target);

        Assert.Equal(1, tracker.GetUsage().MonthlyUsed);
    }

    [Fact(Timeout = 5000)]
    public async Task Records_calls_with_correct_weight()
    {
        var provider = new FakeProvider(new BudgetRate(6000, 100));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        await gate.AcquireAsync(MakeTarget(3));
        await gate.AcquireAsync(MakeTarget(2));

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
    public async Task Burst_allows_multiple_immediate_acquires()
    {
        var provider = new FakeProvider(new BudgetRate(60, 4));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        for (var i = 0; i < 4; i++)
            await gate.AcquireAsync(MakeTarget(1));

        Assert.Equal(4, tracker.GetUsage().MonthlyUsed);
    }

    private static WeightedTarget MakeTarget(int weight)
    {
        var location = new Njord.Configuration.LocationOptions
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
