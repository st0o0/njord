using Njord.Messages.Polling;

namespace Njord.Polling;

public static class DomainPollStateExtensions
{
    internal static readonly TimeSpan SteadyBuffer = TimeSpan.FromSeconds(30);

    public static DomainPollState RecordHashChange(
        this DomainPollState domain, int hash, DateTimeOffset now)
    {
        var updated = domain with
        {
            Phase = PollPhase.Active,
            LastHash = hash,
            LastChanged = now,
            ConsecutiveFailures = 0,
            NextPollAt = now + domain.EffectiveInterval - SteadyBuffer,
        };

        if (domain.LastChanged is { } previous)
        {
            var observed = now - previous;
            updated = updated.UpdateDrift(observed);
        }

        return updated;
    }

    public static DomainPollState RecordHashUnchanged(
        this DomainPollState domain, DateTimeOffset now)
    {
        return domain with
        {
            ConsecutiveFailures = 0,
            NextPollAt = now + domain.EffectiveInterval,
        };
    }
}
