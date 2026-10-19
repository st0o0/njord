using System.Collections.Immutable;
using Njord.Messages.Forecast;
using Njord.Model;

namespace Njord.Pipeline;

public sealed record PollRoundState(
    Guid RoundId,
    ImmutableDictionary<DomainKind, FetchDomainResponse> Results,
    ImmutableHashSet<DomainKind> Pending)
{
    public static PollRoundState Start(Guid roundId, IEnumerable<DomainKind> domains) =>
        new(roundId, ImmutableDictionary<DomainKind, FetchDomainResponse>.Empty, [.. domains]);
}

public static class PollRoundStateExtensions
{
    public static PollWorkerState
        StartRound(this PollWorkerState state, Guid roundId, IEnumerable<DomainKind> domains) =>
        state with { Round = PollRoundState.Start(roundId, domains) };

    public static bool IsCurrentRound(this PollWorkerState state, Guid roundId) => state.Round?.RoundId == roundId;

    public static PollWorkerState RecordReply(this PollWorkerState state, Guid roundId, DomainKind domain,
        FetchDomainResponse reply) =>
        state.Round is { } round && round.RoundId == roundId
            ? state with
            {
                Round = round with
                {
                    Results = round.Results.SetItem(domain, reply),
                    Pending = round.Pending.Remove(domain)
                }
            }
            : state;

    public static PollWorkerState FailPending(this PollWorkerState state, Func<DomainKind, FetchDomainResponse> failure)
    {
        if (state.Round is not { } round)
        {
            return state;
        }

        var results = round.Results;
        foreach (var domain in round.Pending)
        {
            results = results.SetItem(domain, failure(domain));
        }

        return state with { Round = round with { Results = results, Pending = [] } };
    }

    public static PollWorkerState EndRound(this PollWorkerState state) => state with { Round = null };

    // The reply that stands for the whole round. Priority: changed > unchanged > rate limit > budget >
    // pipeline unavailable > any other failure.
    public static FetchDomainResponse ChooseAggregateReply(this PollWorkerState state,
        IEnumerable<FetchDomainResponse> results)
    {
        var replies = results.ToList();
        var completed = replies.OfType<FetchDomainCompleted>().ToList();
        var changed = completed.FirstOrDefault(c => c.Hash != state.GetDomainHash(c.Domain));
        if (changed is not null)
        {
            return changed;
        }

        if (completed.Count > 0)
        {
            return completed[0];
        }

        var rateLimited = replies.OfType<FetchDomainFailed>()
            .FirstOrDefault(f => f.Reason == PollFailureReason.RateLimited);
        if (rateLimited is not null)
        {
            return rateLimited;
        }

        var rejected = replies.OfType<FetchDomainRejected>().ToList();
        var budget = rejected.FirstOrDefault(r => r.Reason == FetchRejectionReason.Budget);
        if (budget is not null)
        {
            return budget;
        }

        var unavailable = rejected.FirstOrDefault(r => r.Reason == FetchRejectionReason.PipelineUnavailable);
        if (unavailable is not null)
        {
            return unavailable;
        }

        return replies[0];
    }
}
