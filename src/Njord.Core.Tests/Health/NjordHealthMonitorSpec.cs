using Njord.Core.Health;

namespace Njord.Core.Tests.Health;

public sealed class NjordHealthMonitorSpec
{
    private static readonly DateTimeOffset Started = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_constructor_sets_the_start_time_and_the_initial_budget()
    {
        var monitor = new NjordHealthMonitor(Started, new BudgetHealth(0, 0, 10_000, 300_000));

        Assert.Multiple(
            () => Assert.Equal(Started, monitor.ServiceStartedUtc),
            () => Assert.Equal(new BudgetHealth(0, 0, 10_000, 300_000), monitor.Budget),
            () => Assert.Equal(new MqttHealth(false, null), monitor.Mqtt),
            () => Assert.Equal(new PollHealth(null), monitor.Poll));
    }

    [Fact]
    public void Updates_replace_the_health_records()
    {
        var monitor = new NjordHealthMonitor(Started, new BudgetHealth(0, 0, 0, 0));

        monitor.UpdateMqtt(new MqttHealth(true, null));
        monitor.UpdatePoll(new PollHealth(Started));
        monitor.UpdateBudget(new BudgetHealth(1, 2, 3, 4));

        Assert.Multiple(
            () => Assert.Equal(new MqttHealth(true, null), monitor.Mqtt),
            () => Assert.Equal(new PollHealth(Started), monitor.Poll),
            () => Assert.Equal(new BudgetHealth(1, 2, 3, 4), monitor.Budget));
    }
}
