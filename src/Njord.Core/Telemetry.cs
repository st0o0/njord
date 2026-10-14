using System.Diagnostics.Metrics;

namespace Njord.Core;

internal static class Telemetry
{
    internal static readonly Meter Meter = new("Njord.Core");

    internal static readonly Counter<long> StreamConsumerRewires =
        Meter.CreateCounter<long>("njord_stream_consumer_rewires_total", "{rewire}");
}
