using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.Time.Testing;
using Njord.Messages.Budget;
using Njord.OpenMeteo.Budget;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class GatewayBudgetGateTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private readonly FakeTimeProvider _time = new FakeTimeProvider(TestTimeConstants.PipelineEpoch);
    private readonly SwitchableBudgetProvider _rate = new() { CostPerMinute = 60, MaxBurst = 16 };

    private (GatewayBudgetGate Gate, TestProbe Tracker) CreateGate(int maxCallWeight = 1)
    {
        var tracker = CreateTestProbe();
        return (new GatewayBudgetGate(_rate, tracker.Ref, _time, maxCallWeight), tracker);
    }

    [Fact(Timeout = 30000)]
    public async Task An_acquisition_within_the_burst_is_reported_to_the_tracker()
    {
        var (gate, tracker) = CreateGate();

        var acquired = gate.TryAcquire(10);

        Assert.True(acquired);
        Assert.Equal(new RecordApiCall(10), await tracker.ExpectMsgAsync<RecordApiCall>(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 30000)]
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
    public void Max_burst_is_raised_to_max_call_weight_when_base_burst_is_lower()
    {
        var (gate, _) = CreateGate(maxCallWeight: 18);

        Assert.True(gate.TryAcquire(18));
    }
}
