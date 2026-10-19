using Akka.TestKit;
using Microsoft.Extensions.Time.Testing;
using Njord.Messages.Budget;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class GatewayBudgetGateSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly SwitchableBudgetProvider _rate = new() { CostPerMinute = 60, MaxBurst = 16 };

    protected override bool UseTestPersistence => false;

    private (GatewayBudgetGate Gate, TestProbe Tracker) CreateGate()
    {
        var tracker = CreateTestProbe();
        return (new GatewayBudgetGate(_rate, tracker.Ref, _time), tracker);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_acquisition_within_the_burst_is_reported_to_the_tracker()
    {
        var (gate, tracker) = CreateGate();

        var acquired = gate.TryAcquire(10);

        Assert.True(acquired);
        Assert.Equal(new RecordApiCall(10), await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: TestContext.Current.CancellationToken));
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
    public void The_estimated_delay_is_the_token_deficit_over_the_refill_rate()
    {
        var (gate, _) = CreateGate();
        Assert.True(gate.TryAcquire(16));

        Assert.Equal(TimeSpan.FromSeconds(3), gate.EstimateDelay(3));
    }
}
