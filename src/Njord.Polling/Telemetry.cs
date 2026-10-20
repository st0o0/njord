using System.Diagnostics.Metrics;
using Njord.Forecast;
using Njord.Persistence.Events.Polling;

namespace Njord.Polling;

internal static class Telemetry
{
    public const string MeterName = "Njord.Polling";

    internal static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> DataChanged =
        Meter.CreateCounter<long>("njord_pipeline_data_changed_total", "{change}");

    internal static readonly Counter<long> DomainPolls =
        Meter.CreateCounter<long>("njord_pipeline_domain_polls_total", "{poll}");

    internal static readonly Counter<long> CycleLearned =
        Meter.CreateCounter<long>("njord_pipeline_cycle_learned_total", "{cycle}");

    internal static void RecordDomainPoll(DomainKind domain, PersistedDomainPollOutcome outcome) =>
        DomainPolls.Add(1, DomainTag(domain), new KeyValuePair<string, object?>("outcome", outcome switch
        {
            PersistedDomainPollOutcome.Changed => "changed",
            PersistedDomainPollOutcome.Unchanged => "unchanged",
            _ => "failed",
        }));

    internal static void RecordCycleLearned(DomainKind domain) => CycleLearned.Add(1, DomainTag(domain));

    private static KeyValuePair<string, object?> DomainTag(DomainKind domain) => new("domain", domain switch
    {
        DomainKind.Weather => "weather",
        DomainKind.AirQuality => "air_quality",
        DomainKind.Flood => "flood",
        DomainKind.Solar => "solar",
        DomainKind.Marine => "marine",
        DomainKind.Seasonal => "seasonal",
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown domain kind"),
    });
}
