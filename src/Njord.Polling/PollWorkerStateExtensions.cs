using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;

namespace Njord.Polling;

public static class PollWorkerStateExtensions
{
    public static PollWorkerState Apply(this PollWorkerState state, PollingStarted evt)
    {
        if (state.Target is null)
        {
            var domains = state.Domains;
            foreach (var d in evt.Domains)
            {
                var dk = (DomainKind)d;
                if (!domains.ContainsKey(dk))
                {
                    domains = domains.SetItem(dk, DomainPollState.Empty(dk) with { NextPollAt = evt.Utc });
                }
            }

            return state with
            {
                Target = new PollTarget(new Location(evt.Location, 0, 0)),
                Active = true,
                DiscoveryInterval = evt.DiscoveryInterval,
                Domains = domains,
            };
        }

        if (evt.DiscoveryInterval != state.DiscoveryInterval)
        {
            // Replan discovery-phase domains to the new interval
            var domains = state.Domains;
            foreach (var (dk, ds) in state.Domains)
            {
                if (ds is { Phase: PollPhase.Discovery, NextPollAt: { } nextPoll } && nextPoll > evt.Utc)
                {
                    var replanned = nextPoll - state.DiscoveryInterval + evt.DiscoveryInterval;
                    var next = replanned < evt.Utc ? evt.Utc : replanned;
                    domains = domains.SetItem(dk, ds with { NextPollAt = next });
                }
            }

            return state with { Active = true, DiscoveryInterval = evt.DiscoveryInterval, Domains = domains };
        }

        return state with { Active = true };
    }

    public static PollWorkerState Apply(this PollWorkerState state, PollingStopped _) =>
        state with { Active = false };

    public static PollWorkerState Apply(this PollWorkerState state, DomainPolled evt)
    {
        var domain = (DomainKind)evt.Domain;
        var ds = state.GetDomain(domain);

        ds = evt.Outcome switch
        {
            PersistedDomainPollOutcome.Changed => ds.RecordHashChange(evt.Hash!.Value, evt.Utc, state.DiscoveryInterval),
            PersistedDomainPollOutcome.Unchanged => ds.RecordHashUnchanged(evt.Utc, state.DiscoveryInterval),
            PersistedDomainPollOutcome.Failed => ds with { NextPollAt = evt.Utc + (evt.Delay ?? TimeSpan.FromMinutes(1)) },
            _ => throw new ArgumentOutOfRangeException(nameof(evt), $"Unknown outcome: {evt.Outcome}"),
        };

        ds = ds with { ConsecutiveFailures = evt.ConsecutiveFailures };

        return state with { Domains = state.Domains.SetItem(domain, ds) };
    }

    public static DomainPollState GetDomain(this PollWorkerState state, DomainKind domain) =>
        state.Domains.TryGetValue(domain, out var ds) ? ds : DomainPollState.Empty(domain);

    public static IReadOnlyList<DomainKind> DueDomainsAt(
        this PollWorkerState state, IEnumerable<DomainKind> enabled, DateTimeOffset now) =>
        [.. enabled.Where(d => !state.Domains.TryGetValue(d, out var ds) || !ds.NextPollAt.HasValue || ds.NextPollAt.Value <= now)];

    public static int? GetDomainHash(this PollWorkerState state, DomainKind domain) =>
        state.Domains.TryGetValue(domain, out var ds) ? ds.LastHash : null;
}
