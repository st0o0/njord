namespace Njord.Core.Health;

public sealed record MqttHealthSnapshot(bool IsConnected, DateTimeOffset? DisconnectedSince);

public sealed record PollHealthSnapshot(DateTimeOffset? LastSuccessfulPollUtc);

public sealed record BudgetHealthSnapshot(long UsedDaily, long UsedMonthly, long LimitDaily, long LimitMonthly);

public sealed class NjordHealthMonitor(DateTimeOffset serviceStartedUtc, BudgetHealthSnapshot initialBudget)
{
    private MqttHealthSnapshot _mqtt = new(false, null);
    private PollHealthSnapshot _poll = new(null);
    private BudgetHealthSnapshot _budget = initialBudget;

    public DateTimeOffset ServiceStartedUtc { get; } = serviceStartedUtc;

    public MqttHealthSnapshot Mqtt => Volatile.Read(ref _mqtt);
    public PollHealthSnapshot Poll => Volatile.Read(ref _poll);
    public BudgetHealthSnapshot Budget => Volatile.Read(ref _budget);

    public void UpdateMqtt(MqttHealthSnapshot snapshot) => Volatile.Write(ref _mqtt, snapshot);
    public void UpdatePoll(PollHealthSnapshot snapshot) => Volatile.Write(ref _poll, snapshot);
    public void UpdateBudget(BudgetHealthSnapshot snapshot) => Volatile.Write(ref _budget, snapshot);
}
