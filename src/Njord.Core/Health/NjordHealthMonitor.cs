namespace Njord.Core.Health;

public sealed record MqttHealth(bool IsConnected, DateTimeOffset? DisconnectedSince);

public sealed record PollHealth(DateTimeOffset? LastSuccessfulPollUtc);

public sealed record BudgetHealth(long UsedDaily, long UsedMonthly, long LimitDaily, long LimitMonthly);

public sealed class NjordHealthMonitor(DateTimeOffset serviceStartedUtc, BudgetHealth initialBudget)
{
    private MqttHealth _mqtt = new(false, null);
    private PollHealth _poll = new(null);
    private BudgetHealth _budget = initialBudget;

    public DateTimeOffset ServiceStartedUtc { get; } = serviceStartedUtc;

    public MqttHealth Mqtt => Volatile.Read(ref _mqtt);
    public PollHealth Poll => Volatile.Read(ref _poll);
    public BudgetHealth Budget => Volatile.Read(ref _budget);

    public void UpdateMqtt(MqttHealth health) => Volatile.Write(ref _mqtt, health);
    public void UpdatePoll(PollHealth health) => Volatile.Write(ref _poll, health);
    public void UpdateBudget(BudgetHealth health) => Volatile.Write(ref _budget, health);
}
