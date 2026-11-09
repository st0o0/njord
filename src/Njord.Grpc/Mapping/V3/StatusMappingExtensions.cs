using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using ProtoPollPhase = Njord.Grpc.V3.PollPhase;

namespace Njord.Grpc.Mapping.V3;

internal static class StatusMappingExtensions
{
    public static ProtoPollPhase ToProto(this PollPhase phase) => phase switch
    {
        PollPhase.Discovery => ProtoPollPhase.Discovery,
        PollPhase.Steady => ProtoPollPhase.Steady,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    public static Grpc.V3.ModelStatus ToModelStatus(this PollStateEntry entry)
    {
        var status = new Grpc.V3.ModelStatus
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

    public static Grpc.V3.TriggerPollResponse ToProto(this TriggerImmediatePollCompleted completed)
    {
        var response = new Grpc.V3.TriggerPollResponse { TriggeredCount = completed.Count };
        response.Targets.AddRange(completed.Targets);
        return response;
    }

    public static Grpc.V3.BudgetStatus ToProto(this BudgetStatusResult status) =>
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
