using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using ProtoPollPhase = Njord.Grpc.Proto.PollPhase;

namespace Njord.Grpc.Mapping;

internal static class StatusMappingExtensions
{
    public static ProtoPollPhase ToProto(this PollPhase phase) => phase switch
    {
        PollPhase.Discovery => ProtoPollPhase.Discovery,
        PollPhase.Steady => ProtoPollPhase.Steady,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    public static Proto.ModelStatus ToModelStatus(this PollStateEntry entry)
    {
        var status = new Proto.ModelStatus
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

    public static Proto.TriggerPollResponse ToProto(this TriggerImmediatePollCompleted completed)
    {
        var response = new Proto.TriggerPollResponse { TriggeredCount = completed.Count };
        response.Targets.AddRange(completed.Targets);
        return response;
    }

    public static Proto.BudgetStatus ToProto(this BudgetStatusResult status) =>
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
