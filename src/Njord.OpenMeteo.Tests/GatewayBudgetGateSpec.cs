using Akka.TestKit;
using Microsoft.Extensions.Time.Testing;
using Njord.Messages.Budget;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class GatewayBudgetGateSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly MutableRate _rate = new(new BudgetRate(CostPerMinute: 60, MaxBurst: 16));

    protected override bool UseTestPersistence => false;

    private (GatewayBudgetGate Gate, TestProbe Tracker) CreateGate()
    {
        var tracker = CreateTestProbe();
        return (new GatewayBudgetGate(_rate, tracker.Ref, _time), tracker);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_new_gate_admits_a_full_burst_and_reports_each_acquisition_to_the_tracker()
    {
        var ct = TestContext.Current.CancellationToken;
        var (gate, tracker) = CreateGate();

        var acquired = gate.TryAcquire(10);
        var second = gate.TryAcquire(6);

        Assert.Multiple(
            () => Assert.True(acquired),
            () => Assert.True(second));
        Assert.Equal(new RecordApiCall(10), await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: ct));
        Assert.Equal(new RecordApiCall(6), await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: ct));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_exhausted_gate_refuses_without_telling_the_tracker()
    {
        var ct = TestContext.Current.CancellationToken;
        var (gate, tracker) = CreateGate();
        Assert.True(gate.TryAcquire(16));
        await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: ct);

        var refused = gate.TryAcquire(1);

        Assert.False(refused);
        await tracker.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), ct);
    }

    [Fact]
    public void Tokens_refill_with_the_elapsed_time_at_the_configured_rate()
    {
        var (gate, _) = CreateGate();
        Assert.True(gate.TryAcquire(16));

        _time.Advance(TimeSpan.FromSeconds(4));

        Assert.Multiple(
            () => Assert.True(gate.TryAcquire(4)),
            () => Assert.False(gate.TryAcquire(1)));
    }

    [Fact]
    public void The_refill_never_exceeds_the_burst_size()
    {
        var (gate, _) = CreateGate();
        Assert.True(gate.TryAcquire(16));

        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Multiple(
            () => Assert.True(gate.TryAcquire(16)),
            () => Assert.False(gate.TryAcquire(1)));
    }

    [Fact]
    public void The_estimated_delay_is_the_token_deficit_over_the_refill_rate()
    {
        var (gate, _) = CreateGate();
        Assert.True(gate.TryAcquire(16));

        Assert.Equal(TimeSpan.FromSeconds(3), gate.EstimateDelay(3));
    }

    [Fact]
    public void No_delay_is_estimated_while_enough_tokens_remain()
    {
        var (gate, _) = CreateGate();

        Assert.Equal(TimeSpan.Zero, gate.EstimateDelay(16));
    }

    [Fact]
    public void A_tiny_deficit_is_rounded_up_to_ten_milliseconds()
    {
        _rate.Current = new BudgetRate(CostPerMinute: 60_000, MaxBurst: 16);
        var (gate, _) = CreateGate();
        Assert.True(gate.TryAcquire(16));
        _time.Advance(TimeSpan.FromMilliseconds(15));

        Assert.Equal(TimeSpan.FromMilliseconds(10), gate.EstimateDelay(2));
    }

    [Fact]
    public void A_changed_rate_applies_once_the_refresh_interval_has_passed()
    {
        var (gate, _) = CreateGate();
        Assert.True(gate.TryAcquire(16));
        _rate.Current = new BudgetRate(CostPerMinute: 120, MaxBurst: 16);

        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.Multiple(
            () => Assert.True(gate.TryAcquire(10)),
            () => Assert.False(gate.TryAcquire(1)));
    }

    [Fact]
    public void A_changed_rate_is_ignored_before_the_refresh_interval_has_passed()
    {
        var (gate, _) = CreateGate();
        Assert.True(gate.TryAcquire(16));
        _rate.Current = new BudgetRate(CostPerMinute: 120, MaxBurst: 16);

        _time.Advance(TimeSpan.FromSeconds(4));

        Assert.Multiple(
            () => Assert.True(gate.TryAcquire(4)),
            () => Assert.False(gate.TryAcquire(1)));
    }

    private sealed class MutableRate(BudgetRate initial) : IBudgetProvider
    {
        public BudgetRate Current { get; set; } = initial;

        public BudgetRate GetCurrentRate() => Current;
    }
}
