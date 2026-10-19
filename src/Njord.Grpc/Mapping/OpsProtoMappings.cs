using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.V2;
using Njord.Messages.Budget;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class OpsProtoMappings
{
    public static ModelStatus ToModelStatus(this PollStateEntry entry)
    {
        var status = new ModelStatus
        {
            Location = entry.Location.Name,
            Phase = entry.Phase.ToProto(),
            NextPoll = entry.NextPollUtc.ToTimestamp(),
            MissCount = entry.MissCount,
        };
        if (entry.LastChangeUtc is { } lastChange)
        {
            status.LastChange = lastChange.ToTimestamp();
        }

        if (entry.CycleSeconds is { } cycleSeconds)
        {
            status.CycleSeconds = cycleSeconds;
        }

        return status;
    }

    public static TriggerTarget ToTriggerTarget(this PollStateEntry entry)
    {
        var target = new TriggerTarget
        {
            Location = entry.Location.Name,
            Phase = entry.Phase.ToProto(),
            NextPoll = entry.NextPollUtc.ToTimestamp(),
            MissCount = entry.MissCount,
        };
        if (entry.LastChangeUtc is { } lastChange)
        {
            target.LastChange = lastChange.ToTimestamp();
        }

        if (entry.CycleSeconds is { } cycleSeconds)
        {
            target.CycleSeconds = cycleSeconds;
        }

        return target;
    }

    public static TriggerPollResponse ToProto(this TriggerImmediatePollCompleted completed)
    {
        var response = new TriggerPollResponse { TriggeredCount = completed.Count };
        response.Targets.AddRange(completed.Targets);
        return response;
    }

    public static BudgetStatus ToProto(this BudgetStatusResult status) =>
        new()
        {
            MonthlyLimit = status.MonthlyLimit,
            MonthlyUsed = status.MonthlyUsed,
            DailyLimit = status.DailyLimit,
            DailyUsed = status.DailyUsed,
            UsagePercent = status.MonthlyLimit > 0
                ? (double)status.MonthlyUsed / status.MonthlyLimit * 100
                : 0,
        };
}
