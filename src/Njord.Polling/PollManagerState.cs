using System.Collections.Immutable;
using Akka.Actor;
using Njord.Core;
using Njord.Messages.Polling;

namespace Njord.Polling;

internal sealed record PollManagerConfig(
    IReadOnlyList<PollTarget> Targets,
    IReadOnlyDictionary<string, PollTarget> TargetsById)
{
    public static PollManagerConfig Create(IReadOnlyList<Location> locations)
    {
        var targets = new List<PollTarget>();
        var targetsById = new Dictionary<string, PollTarget>(StringComparer.OrdinalIgnoreCase);

        foreach (var location in locations)
        {
            var target = new PollTarget(location);
            targets.Add(target);
            targetsById[target.Id] = target;
        }

        return new PollManagerConfig(targets, targetsById);
    }
}

internal sealed record PollManagerState(
    ImmutableDictionary<string, PollStateEntry> States,
    ImmutableDictionary<IActorRef, string> Watched,
    ImmutableDictionary<string, int> RestartAttempts,
    TimeSpan DiscoveryInterval,
    IActorRef? Region,
    int ResolveAttempts,
    DateTimeOffset StartedUtc,
    DateTimeOffset? LastSuccessfulPollUtc)
{
    public static PollManagerState Create(
        PollManagerConfig config, TimeSpan discoveryInterval, DateTimeOffset now)
    {
        var states = config.Targets.ToImmutableDictionary(
            t => t.Id,
            t => new PollStateEntry(t.Location, PollPhase.Discovery, now, null, 0, null),
            StringComparer.OrdinalIgnoreCase);

        return new PollManagerState(
            states,
            ImmutableDictionary<IActorRef, string>.Empty,
            ImmutableDictionary.Create<string, int>(StringComparer.OrdinalIgnoreCase),
            discoveryInterval,
            null,
            0,
            now,
            null);
    }
}
