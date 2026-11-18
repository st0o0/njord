using System.Collections.Immutable;
using Njord.Core;
using Njord.Messages.Polling;

namespace Njord.Polling;

public sealed record DomainPollState(
    DomainKind Domain,
    PollPhase Phase,
    int? LastHash,
    DateTimeOffset? LastChanged,
    TimeSpan PollInterval,
    DateTimeOffset? NextPollAt,
    int ConsecutiveFailures = 0,
    TimeSpan? ObservedIntervalEma = null,
    int DriftObservationCount = 0)
{
    private const double DriftAlpha = 0.3;
    private const int MinDriftObservations = 3;
    private const double DriftFloorFactor = 0.5;
    private const double DriftCeilingFactor = 2.0;

    public static DomainPollState Empty(DomainKind domain, TimeSpan pollInterval) =>
        new(domain, PollPhase.Active, null, null, pollInterval, null);

    public TimeSpan EffectiveInterval =>
        DriftObservationCount >= MinDriftObservations && ObservedIntervalEma is { } ema
            ? Clamp(ema, PollInterval)
            : PollInterval;

    internal DomainPollState UpdateDrift(TimeSpan observed)
    {
        var clamped = Clamp(observed, PollInterval);
        var newEma = ObservedIntervalEma is { } current
            ? TimeSpan.FromSeconds(DriftAlpha * clamped.TotalSeconds + (1 - DriftAlpha) * current.TotalSeconds)
            : clamped;
        return this with { ObservedIntervalEma = newEma, DriftObservationCount = DriftObservationCount + 1 };
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan configured)
    {
        var floor = TimeSpan.FromSeconds(configured.TotalSeconds * DriftFloorFactor);
        var ceiling = TimeSpan.FromSeconds(configured.TotalSeconds * DriftCeilingFactor);
        if (value < floor)
        {
            return floor;
        }

        if (value > ceiling)
        {
            return ceiling;
        }

        return value;
    }
}

public sealed record PollWorkerState(
    PollTarget? Target,
    bool Active,
    ImmutableDictionary<DomainKind, DomainPollState> Domains)
{
    public static readonly PollWorkerState Empty = new(null, false, []);

    public static readonly TimeSpan RateLimitMinDelay = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PipelineUnavailableRetry = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan SuspendedRecheckInterval = TimeSpan.FromHours(24);

    public DateTimeOffset NextPollUtc
    {
        get
        {
            var upcoming = Domains.Values.Where(d => d.NextPollAt.HasValue).Select(d => d.NextPollAt!.Value).ToList();
            return upcoming.Count > 0 ? upcoming.Min() : DateTimeOffset.MinValue;
        }
    }
}
