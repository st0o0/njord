using System.Collections.Immutable;
using Akka.Actor;
using Njord.Core;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;

namespace Njord.Pipeline;

internal static class PollRounds
{
    public static readonly TimeSpan DefaultRetry = TimeSpan.FromMinutes(1);

    public static List<object> ToDomainEvents(
        IReadOnlyDictionary<DomainKind, FetchDomainResponse> results,
        DateTimeOffset now,
        Func<DomainKind, int?> lastHash) =>
        [.. results.OrderBy(kv => kv.Key).Select(kv => (object)(kv.Value switch
        {
            FetchDomainCompleted c when c.Hash != lastHash(kv.Key) => new DomainPolled((int)kv.Key, 0, c.Hash, now, null),
            FetchDomainCompleted => new DomainPolled((int)kv.Key, 1, null, now, null),
            var failure => new DomainPolled((int)kv.Key, 2, null, now, FailureDelay(failure)),
        }))];

    public static TimeSpan FailureDelay(FetchDomainResponse reply) => reply switch
    {
        FetchDomainFailed { Reason: PollFailureReason.RateLimited } => PollWorkerState.RateLimitMinDelay,
        FetchDomainRejected { Reason: FetchRejectionReason.Budget } => PollWorkerState.RateLimitMinDelay,
        FetchDomainRejected { Reason: FetchRejectionReason.PipelineUnavailable } => PollWorkerState.PipelineUnavailableRetry,
        _ => DefaultRetry,
    };

    public static PollWorkerOutcome ChooseOutcome(IEnumerable<object> domainEvents)
    {
        var outcomes = domainEvents.OfType<DomainPolled>().Select(p => p.Outcome).Where(o => o is 0 or 1).ToList();
        return outcomes.Count == 0 ? PollWorkerOutcome.Failed
            : outcomes.Contains(0) ? PollWorkerOutcome.Changed : PollWorkerOutcome.Unchanged;
    }

    public static Task<FetchDomainResponse> AskDomain(
        this IActorRef gateway, Location location, DomainKind domain, TimeSpan timeout) =>
        gateway.Ask<FetchDomainResponse>(new FetchDomain(location, domain), timeout);

    public static FetchDomainFailed AskFailure(Location location, DomainKind domain, TimeSpan timeout, Exception ex) =>
        new(location, domain, PollFailureReason.Transport, ex is AskTimeoutException
            ? $"Gateway did not answer within {timeout}"
            : $"Gateway request failed ({ex.GetType().Name}: {ex.Message})");

    public static ImmutableDictionary<DomainKind, FetchDomainResponse> TimeOut(
        this ImmutableDictionary<DomainKind, FetchDomainResponse> results,
        IEnumerable<DomainKind> pending,
        Location location) =>
        pending.Aggregate(results, (acc, domain) => acc.SetItem(domain,
            new FetchDomainFailed(location, domain, PollFailureReason.Transport, "Round timed out")));
}
