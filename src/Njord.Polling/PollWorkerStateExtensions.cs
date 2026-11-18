using Njord.Core;
using Njord.Messages.Polling;
using Njord.Persistence.Events.Polling;

namespace Njord.Polling;

public static class PollWorkerStateExtensions
{
    public static PollWorkerState Apply(this PollWorkerState state, PollingStarted evt)
    {
        var target = new PollTarget(new Location(evt.Location, evt.Latitude ?? 0, evt.Longitude ?? 0));
        var domains = state.Domains;

        foreach (var d in evt.Domains)
        {
            var dk = (DomainKind)d;
            var interval = evt.DomainIntervals.TryGetValue(d, out var ts) ? ts : TimeSpan.FromMinutes(60);
            if (!domains.TryGetValue(dk, out var existing))
            {
                domains = domains.SetItem(dk, DomainPollState.Empty(dk, interval) with { NextPollAt = evt.Utc });
            }
            else
            {
                domains = domains.SetItem(dk, existing with { PollInterval = interval });
            }
        }

        return state with { Target = target, Active = true, Domains = domains };
    }

    public static PollWorkerState Apply(this PollWorkerState state, PollingStopped _) =>
        state with { Active = false };

    public static PollWorkerState Apply(this PollWorkerState state, DomainPolled evt)
    {
        var domain = (DomainKind)evt.Domain;
        var ds = state.GetDomain(domain);

        ds = evt.Outcome switch
        {
            PersistedDomainPollOutcome.Changed => ds.RecordHashChange(evt.Hash!.Value, evt.Utc),
            PersistedDomainPollOutcome.Unchanged => ds.RecordHashUnchanged(evt.Utc),
            PersistedDomainPollOutcome.Failed => ds with
            {
                NextPollAt = evt.Utc + (evt.Delay ?? TimeSpan.FromMinutes(1)),
                ConsecutiveFailures = evt.ConsecutiveFailures,
            },
            PersistedDomainPollOutcome.NoData => ds with
            {
                Phase = PollPhase.Suspended,
                NextPollAt = evt.Utc + (evt.Delay ?? PollWorkerState.SuspendedRecheckInterval),
                ConsecutiveFailures = evt.ConsecutiveFailures,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(evt), $"Unknown outcome: {evt.Outcome}"),
        };

        return state with { Domains = state.Domains.SetItem(domain, ds) };
    }

    public static DomainPollState GetDomain(this PollWorkerState state, DomainKind domain) =>
        state.Domains.TryGetValue(domain, out var ds) ? ds : DomainPollState.Empty(domain, TimeSpan.FromMinutes(60));

    public static IReadOnlyList<DomainKind> DueDomainsAt(
        this PollWorkerState state, IEnumerable<DomainKind> enabled, DateTimeOffset now) =>
        [.. enabled.Where(d => !state.Domains.TryGetValue(d, out var ds) || !ds.NextPollAt.HasValue || ds.NextPollAt.Value <= now)];

    public static int? GetDomainHash(this PollWorkerState state, DomainKind domain) =>
        state.Domains.TryGetValue(domain, out var ds) ? ds.LastHash : null;
}
