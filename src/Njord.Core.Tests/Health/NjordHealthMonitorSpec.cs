using Njord.Core.Health;

namespace Njord.Core.Tests.Health;

public sealed class NjordHealthMonitorSpec
{
    private static readonly DateTimeOffset Started = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_constructor_sets_the_start_time_and_the_initial_budget()
    {
        var monitor = new NjordHealthMonitor(Started, new BudgetHealthSnapshot(0, 0, 10_000, 300_000));

        Assert.Multiple(
            () => Assert.Equal(Started, monitor.ServiceStartedUtc),
            () => Assert.Equal(new BudgetHealthSnapshot(0, 0, 10_000, 300_000), monitor.Budget),
            () => Assert.Equal(new MqttHealthSnapshot(false, null), monitor.Mqtt),
            () => Assert.Equal(new PollHealthSnapshot(null), monitor.Poll));
    }

    [Fact]
    public void Updates_replace_the_snapshots()
    {
        var monitor = new NjordHealthMonitor(Started, new BudgetHealthSnapshot(0, 0, 0, 0));

        monitor.UpdateMqtt(new MqttHealthSnapshot(true, null));
        monitor.UpdatePoll(new PollHealthSnapshot(Started));
        monitor.UpdateBudget(new BudgetHealthSnapshot(1, 2, 3, 4));

        Assert.Multiple(
            () => Assert.Equal(new MqttHealthSnapshot(true, null), monitor.Mqtt),
            () => Assert.Equal(new PollHealthSnapshot(Started), monitor.Poll),
            () => Assert.Equal(new BudgetHealthSnapshot(1, 2, 3, 4), monitor.Budget));
    }
}
