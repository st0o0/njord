using Njord.Core;
using Njord.Core.Polling;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

internal static class PollStateFixture
{
    public static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    public static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    private static readonly int[] _forecastOnly = [(int)DomainKind.Forecast];

    private static readonly int[] _allDomains =
        [(int)DomainKind.Forecast, (int)DomainKind.AirQuality, (int)DomainKind.Flood, (int)DomainKind.Marine];

    public static Dictionary<int, TimeSpan> IntervalsFor(params (DomainKind Domain, TimeSpan Interval)[] overrides)
    {
        var intervals = new Dictionary<int, TimeSpan>();
        foreach (var (domain, interval) in overrides)
        {
            intervals[(int)domain] = interval;
        }

        return intervals;
    }

    public static Dictionary<int, TimeSpan> DefaultIntervals(params DomainKind[] domains) =>
        domains.ToDictionary(d => (int)d, d => DomainDefaults.PollInterval(d));

    public static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? forecastInterval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", _forecastOnly,
            IntervalsFor((DomainKind.Forecast, forecastInterval ?? TimeSpan.FromMinutes(60))),
            at ?? T0));

    public static PollWorkerState StartedAll() =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", _allDomains,
            DefaultIntervals(DomainKind.Forecast, DomainKind.AirQuality, DomainKind.Flood, DomainKind.Marine),
            T0));

    public static DomainPolled Changed(DomainKind domain, int hash, DateTimeOffset at) =>
        new((int)domain, PersistedDomainPollOutcome.Changed, hash, at, null);

    public static DomainPolled Unchanged(DomainKind domain, DateTimeOffset at) =>
        new((int)domain, PersistedDomainPollOutcome.Unchanged, null, at, null);

    public static DomainPolled Failed(DomainKind domain, DateTimeOffset at, TimeSpan? delay = null) =>
        new((int)domain, PersistedDomainPollOutcome.Failed, null, at, delay ?? TimeSpan.FromMinutes(1));

    public static DomainPolled NoData(DomainKind domain, DateTimeOffset at) =>
        new((int)domain, PersistedDomainPollOutcome.NoData, null, at, PollWorkerState.SuspendedRecheckInterval);

    public static PollWorkerState WithChanges(DomainKind domain = DomainKind.Forecast) =>
        Started()
            .Apply(Changed(domain, 1, T0))
            .Apply(Changed(domain, 2, T0.AddHours(3)))
            .Apply(Changed(domain, 3, T0.AddHours(6)));
}
