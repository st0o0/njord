using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Messages.Forecast;
using Njord.Tests.Shared;
using Njord.Model;

namespace Njord.OpenMeteo.Tests;

public sealed class GatewayThrottleStageSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();

    protected override bool UseTestPersistence => false;

    private OpenMeteoGateway.FetchTarget Target(TimeSpan receivedAgo, TimeSpan window)
    {
        var received = _time.GetUtcNow() - receivedAgo;
        return new OpenMeteoGateway.FetchTarget(1, new LocationOptions { Name = "lucerne" }, new WeatherModel("icon_d2"),
            new CycleId(received), 1, received + window);
    }

    private async Task<OpenMeteoGateway.Admission> ThrottleAsync(OpenMeteoGateway.FetchTarget target, FakeGate gate, CancellationToken ct) =>
        await Source.Single(target)
            .Via(new GatewayThrottleStage(gate, _time))
            .RunWith(Sink.First<OpenMeteoGateway.Admission>(), Sys.Materializer())
            .WaitAsync(TestTimeouts.RealTimeWaitMax, ct);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_target_the_gate_allows_is_admitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var admission = await ThrottleAsync(Target(TimeSpan.Zero, TimeSpan.FromSeconds(20)), new FakeGate(acquire: true, TimeSpan.Zero), ct);

        Assert.Null(admission.Rejection);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_target_the_budget_alone_cannot_admit_within_its_window_is_rejected_for_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        var admission = await ThrottleAsync(Target(TimeSpan.Zero, TimeSpan.FromSeconds(20)), new FakeGate(acquire: false, TimeSpan.FromSeconds(60)), ct);

        Assert.Equal(FetchRejectionReason.Budget, admission.Rejection);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_target_that_spent_its_window_in_the_queue_is_rejected_as_expired_not_for_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        // 18 s of the 20 s window went by in the queue; a 5 s token wait would have fit the window from receipt.
        var admission = await ThrottleAsync(Target(TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(20)), new FakeGate(acquire: false, TimeSpan.FromSeconds(5)), ct);

        Assert.Equal(FetchRejectionReason.Expired, admission.Rejection);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_target_past_its_deadline_is_rejected_as_expired_without_acquiring()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new FakeGate(acquire: true, TimeSpan.Zero);

        var admission = await ThrottleAsync(Target(TimeSpan.FromSeconds(21), TimeSpan.FromSeconds(20)), gate, ct);

        Assert.Multiple(
            () => Assert.Equal(FetchRejectionReason.Expired, admission.Rejection),
            () => Assert.Equal(0, gate.Acquired));
    }

    private sealed class FakeGate(bool acquire, TimeSpan delay) : IGatewayBudgetGate
    {
        public int Acquired { get; private set; }

        public bool TryAcquire(int weight)
        {
            if (acquire)
            {
                Acquired++;
            }

            return acquire;
        }

        public TimeSpan EstimateDelay(int weight) => delay;
    }
}
