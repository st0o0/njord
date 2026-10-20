using Akka.Actor;
using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollManagerTriggerSpec : PollManagerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_one_location_reaches_its_worker()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new Location("Lucerne", 0, 0)), TestTimeouts.RealTimeWaitMax, ct);

        var trigger = await _region.ExpectMsgAsync<TriggerLocationPoll>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(1, result.Count),
            () => Assert.Equal(["lucerne"], result.Targets),
            () => Assert.Equal("lucerne", trigger.LocationName));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_all_returns_all_targets()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(null), TestTimeouts.RealTimeWaitMax, ct);

        Assert.Multiple(
            () => Assert.Equal(Targets.Length, result.Count),
            () => Assert.Equal(Targets.Select(t => t.ToString()), result.Targets));
        foreach (var target in Targets)
        {
            var trigger = await _region.ExpectMsgAsync<TriggerLocationPoll>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
            Assert.Equal(target.Id, trigger.LocationName);
        }
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_unknown_location_returns_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new Location("nonexistent", 0, 0)), TestTimeouts.RealTimeWaitMax, ct);

        Assert.Multiple(
            () => Assert.Equal(0, result.Count),
            () => Assert.Empty(result.Targets));
        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }
}
