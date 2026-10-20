using System.Diagnostics.Metrics;
using Njord.Model;

namespace Njord.Pipeline;

internal static class Telemetry
{
    public const string MeterName = "Njord.Pipeline";

    internal static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> DataChanged =
        Meter.CreateCounter<long>("njord_pipeline_data_changed_total", "{change}");

    internal static readonly Counter<long> DomainPolls =
        Meter.CreateCounter<long>("njord_pipeline_domain_polls_total", "{poll}");

    internal static readonly Counter<long> CycleLearned =
        Meter.CreateCounter<long>("njord_pipeline_cycle_learned_total", "{cycle}");

    internal static readonly Counter<long> PipelineEvents =
        Meter.CreateCounter<long>("njord_pipeline_events_total", "{event}");

    internal static readonly Histogram<double> EnrichmentDuration =
        Meter.CreateHistogram<double>("njord_pipeline_enrichment_duration_seconds", "s");

    internal static void RecordDomainPoll(DomainKind domain, int outcome) =>
        DomainPolls.Add(1, DomainTag(domain), new KeyValuePair<string, object?>("outcome", outcome switch
        {
            0 => "changed",
            1 => "unchanged",
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
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown domain kind"),
    });
}
