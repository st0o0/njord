using Njord.Core;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

internal static class PollStateFixture
{
    public static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    public static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    public static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    private static readonly int[] ForecastOnly = [(int)DomainKind.Forecast];

    private static readonly int[] AllDomains =
        [(int)DomainKind.Forecast, (int)DomainKind.AirQuality, (int)DomainKind.Flood, (int)DomainKind.Marine];

    public static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? interval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", ForecastOnly, interval ?? Discovery, at ?? T0));

    public static PollWorkerState StartedAll() =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", AllDomains, Discovery, T0));

    public static DomainPolled Changed(DomainKind domain, int hash, DateTimeOffset at) =>
        new((int)domain, PersistedDomainPollOutcome.Changed, hash, at, null);

    public static DomainPolled Unchanged(DomainKind domain, DateTimeOffset at) =>
        new((int)domain, PersistedDomainPollOutcome.Unchanged, null, at, null);

    public static DomainPolled Failed(DomainKind domain, DateTimeOffset at, TimeSpan? delay = null) =>
        new((int)domain, PersistedDomainPollOutcome.Failed, null, at, delay ?? TimeSpan.FromMinutes(1));

    public static PollWorkerState Steady(DomainKind domain = DomainKind.Forecast) =>
        Started()
            .Apply(Changed(domain, 1, T0))
            .Apply(Changed(domain, 2, T0.AddHours(3)))
            .Apply(Changed(domain, 3, T0.AddHours(6)));
}
