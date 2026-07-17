using System.Diagnostics;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Configuration;
using Njord.Pipeline;

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
        var gate = new InstantGate<int>();
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From(Enumerable.Range(0, 5))
            .Via(stage)
            .RunWith(Sink.Seq<int>(), _mat);

        Assert.Equal(5, result.Count);
        Assert.Equal(5, gate.AcquireCount);
    }

    [Fact(Timeout = 5000)]
    public async Task Elements_wait_when_gate_delays()
    {
        var gate = new DelayGate<int>(TimeSpan.FromMilliseconds(100));
        var stage = new BudgetThrottleStage<int>(gate);

        var sw = Stopwatch.StartNew();
        var result = await Source.From(Enumerable.Range(0, 3))
            .Via(stage)
            .RunWith(Sink.Seq<int>(), _mat);
        sw.Stop();

        Assert.Equal(3, result.Count);
        Assert.True(sw.ElapsedMilliseconds >= 250,
            $"3 elements with 100ms delay should take ≥250ms, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact(Timeout = 5000)]
    public async Task Stage_completes_after_pending_element()
    {
        var gate = new DelayGate<int>(TimeSpan.FromMilliseconds(50));
        var stage = new BudgetThrottleStage<int>(gate);

        var result = await Source.From([1, 2, 3])
            .Via(stage)
            .RunWith(Sink.Seq<int>(), _mat);

        Assert.Equal(3, result.Count);
        Assert.Equal([1, 2, 3], result);
    }

    private sealed class InstantGate<T> : IBudgetGate<T>
    {
        public int AcquireCount;

        public Task AcquireAsync(T element, CancellationToken ct = default)
        {
            Interlocked.Increment(ref AcquireCount);
            return Task.CompletedTask;
        }
    }

    private sealed class DelayGate<T>(TimeSpan delay) : IBudgetGate<T>
    {
        public Task AcquireAsync(T element, CancellationToken ct = default)
            => Task.Delay(delay, ct);
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
        var sw = Stopwatch.StartNew();
        await gate.AcquireAsync(target);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 100);
        Assert.Equal(1, tracker.GetUsage().MonthlyUsed);
    }

    [Fact(Timeout = 5000)]
    public async Task Delays_when_tokens_insufficient()
    {
        var provider = new FakeProvider(new BudgetRate(60, 1));
        var tracker = new BudgetTracker();
        var gate = new WeightedBudgetGate(provider, tracker);

        await gate.AcquireAsync(MakeTarget(1));

        var sw = Stopwatch.StartNew();
        await gate.AcquireAsync(MakeTarget(1));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 500,
            $"Second acquire should wait ~1sec at 60/min with burst 1, took {sw.ElapsedMilliseconds}ms");
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
