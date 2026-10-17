using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.V2;
using Njord.Messages.Budget;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using PollPhase = Njord.Grpc.V2.PollPhase;

namespace Njord.Grpc.Mapping;

internal static class OpsProtoMappings
{
    public static ModelStatus ToModelStatus(this PollStateEntry entry)
    {
        var fields = PollFields.From(entry);
        var status = new ModelStatus
        {
            Location = fields.Location,
            Model = fields.Model,
            Phase = fields.Phase,
            NextPoll = fields.NextPoll,
            MissCount = fields.MissCount,
        };
        if (fields.LastChange is { } lastChange)
        {
            status.LastChange = lastChange;
        }

        if (fields.CycleSeconds is { } cycleSeconds)
        {
            status.CycleSeconds = cycleSeconds;
        }

        return status;
    }

    public static TriggerTarget ToTriggerTarget(this PollStateEntry entry)
    {
        var fields = PollFields.From(entry);
        var target = new TriggerTarget
        {
            Location = fields.Location,
            Model = fields.Model,
            Phase = fields.Phase,
            NextPoll = fields.NextPoll,
            MissCount = fields.MissCount,
        };
        if (fields.LastChange is { } lastChange)
        {
            target.LastChange = lastChange;
        }

        if (fields.CycleSeconds is { } cycleSeconds)
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

    public static IEnumerable<string> ActiveAnalyses(this AnalysisSections analysis)
    {
        if (analysis.Consensus.Enabled)
        {
            yield return EnrichmentKind.Consensus.ToWireName();
        }

        if (analysis.Alerts.Enabled)
        {
            yield return EnrichmentKind.Alerts.ToWireName();
        }

        if (analysis.Derived.Enabled)
        {
            yield return EnrichmentKind.Derived.ToWireName();
        }

        if (analysis.Trends.Enabled)
        {
            yield return EnrichmentKind.Trends.ToWireName();
        }

        if (analysis.Indices.Enabled)
        {
            yield return EnrichmentKind.Indices.ToWireName();
        }
    }

    // The fields ModelStatus and TriggerTarget share, mapped once.
    private sealed record PollFields(
        string Location,
        string Model,
        PollPhase Phase,
        Timestamp NextPoll,
        int MissCount,
        Timestamp? LastChange,
        long? CycleSeconds)
    {
        public static PollFields From(PollStateEntry entry) =>
            new(
                entry.Location.Name,
                entry.ModelId,
                entry.Phase.ToProto(),
                Timestamp.FromDateTimeOffset(entry.NextPollUtc),
                entry.MissCount,
                entry.LastChangeUtc is { } lastChange ? Timestamp.FromDateTimeOffset(lastChange) : null,
                entry.CycleSeconds);
    }
}
