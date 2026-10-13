using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.V2;
using Njord.Messages.Pipeline;

namespace Njord.Grpc;

public static class PollStatusMapper
{
    public static ModelStatus ToModelStatus(PollStateEntry entry)
    {
        var status = new ModelStatus
        {
            Location = entry.Location.Name,
            Model = entry.ModelId,
            Phase = PhaseName(entry.Phase),
            NextPoll = Timestamp.FromDateTimeOffset(entry.NextPollUtc),
            MissCount = entry.MissCount,
        };
        if (entry.LastChangeUtc.HasValue)
        {
            status.LastChange = Timestamp.FromDateTimeOffset(entry.LastChangeUtc.Value);
        }

        if (entry.CycleSeconds.HasValue)
        {
            status.CycleSeconds = entry.CycleSeconds.Value;
        }

        return status;
    }

    public static TriggerTarget ToTriggerTarget(PollStateEntry entry)
    {
        var target = new TriggerTarget
        {
            Location = entry.Location.Name,
            Model = entry.ModelId,
            Phase = PhaseName(entry.Phase),
            NextPoll = Timestamp.FromDateTimeOffset(entry.NextPollUtc),
            MissCount = entry.MissCount,
        };
        if (entry.LastChangeUtc.HasValue)
        {
            target.LastChange = Timestamp.FromDateTimeOffset(entry.LastChangeUtc.Value);
        }

        if (entry.CycleSeconds.HasValue)
        {
            target.CycleSeconds = entry.CycleSeconds.Value;
        }

        return target;
    }

    private static string PhaseName(PollPhase phase) => phase == PollPhase.Steady ? "steady" : "discovery";
}
