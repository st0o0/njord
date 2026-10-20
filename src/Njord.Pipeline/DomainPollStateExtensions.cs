using Njord.Messages.Pipeline;

namespace Njord.Pipeline;

// Cycle learning of one domain: Discovery polls every discovery interval and measures the time between hash
// changes; two consistent intervals make the cycle Steady, five misses in Steady send it back to Discovery.
public static class DomainPollStateExtensions
{
    internal static readonly TimeSpan SteadyBuffer = TimeSpan.FromSeconds(30);
    private const double CycleTolerance = 0.2;

    public static DomainPollState RecordHashChange(
        this DomainPollState domain, int hash, DateTimeOffset now, TimeSpan discoveryInterval)
    {
        var changed = domain with { LastHash = hash, LastChanged = now, MissCount = 0 };
        if (domain.LastChanged is not { } previous)
        {
            return changed with { NextPollAt = now + discoveryInterval };
        }

        var interval = now - previous;
        var reference = domain.Phase == PollPhase.Steady ? domain.LearnedCycle : domain.CandidateCycle;
        return reference is { } known && IsConsistent(interval, known)
            ? changed.TransitionToSteady(interval)
            : changed.FallBackToDiscovery(now, discoveryInterval, interval);
    }

    public static DomainPollState RecordHashUnchanged(
        this DomainPollState domain, DateTimeOffset now, TimeSpan discoveryInterval)
    {
        var misses = domain.MissCount + 1;
        if (domain.Phase != PollPhase.Steady)
        {
            return domain with { MissCount = misses, NextPollAt = now + discoveryInterval };
        }

        return misses >= PollWorkerStateExtensions.MaxMissesBeforeFallback
            ? domain.FallBackToDiscovery(now, discoveryInterval)
            : domain with { MissCount = misses, NextPollAt = now + PollWorkerStateExtensions.RetryBackoff(misses) };
    }

    // The next poll is planned from the moment the last change was seen.
    public static DomainPollState TransitionToSteady(this DomainPollState domain, TimeSpan cycle)
    {
        var anchor = domain.LastChanged ?? domain.NextPollAt ?? DateTimeOffset.MinValue;
        var delay = cycle - SteadyBuffer;
        return domain with
        {
            Phase = PollPhase.Steady,
            LearnedCycle = cycle,
            CandidateCycle = cycle,
            MissCount = 0,
            NextPollAt = anchor + (delay > TimeSpan.Zero ? delay : cycle),
        };
    }

    public static DomainPollState FallBackToDiscovery(
        this DomainPollState domain, DateTimeOffset now, TimeSpan discoveryInterval, TimeSpan? candidate = null) =>
        domain with
        {
            Phase = PollPhase.Discovery,
            LearnedCycle = null,
            CandidateCycle = candidate,
            MissCount = 0,
            NextPollAt = now + discoveryInterval,
        };

    private static bool IsConsistent(TimeSpan interval, TimeSpan reference) =>
        Math.Abs((interval - reference).TotalSeconds) <= reference.TotalSeconds * CycleTolerance;
}
