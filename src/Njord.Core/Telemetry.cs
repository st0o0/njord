using System.Diagnostics.Metrics;

namespace Njord.Core;

internal static class Telemetry
{
    public const string MeterName = "Njord.Core";

    internal static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> StreamConsumerRewires =
        Meter.CreateCounter<long>("njord_stream_consumer_rewires_total", "{rewire}");
}
