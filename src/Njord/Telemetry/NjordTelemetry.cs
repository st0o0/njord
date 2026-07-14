using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Njord.Telemetry;

internal static class NjordTelemetry
{
    public const string ServiceName = "njord";

    public static readonly ActivitySource Source = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> PollsTotal =
        Meter.CreateCounter<long>("njord.polls.total");

    public static readonly Counter<long> FetchTotal =
        Meter.CreateCounter<long>("njord.fetch.total");

    public static readonly Counter<long> FetchFailures =
        Meter.CreateCounter<long>("njord.fetch.failures");

    public static readonly Counter<long> MqttPublishes =
        Meter.CreateCounter<long>("njord.mqtt.publishes");

    public static readonly Counter<long> DiscoveryPublishes =
        Meter.CreateCounter<long>("njord.mqtt.discovery");

    public static readonly Counter<long> DataChanges =
        Meter.CreateCounter<long>("njord.data.changes");

    public static readonly Counter<long> Reconnects =
        Meter.CreateCounter<long>("njord.mqtt.reconnects");

    public static readonly Histogram<double> FetchDuration =
        Meter.CreateHistogram<double>("njord.fetch.duration", "ms");

    public static readonly Histogram<double> MqttPublishDuration =
        Meter.CreateHistogram<double>("njord.mqtt.publish.duration", "ms");

    public static readonly UpDownCounter<long> MqttConnected =
        Meter.CreateUpDownCounter<long>("njord.mqtt.connected");
}
