using System.Diagnostics.Metrics;

namespace Njord.Mqtt;

internal static class Telemetry
{
    private static readonly Meter Meter = new("Njord.Mqtt");

    internal static readonly Counter<long> MqttDedup =
        Meter.CreateCounter<long>("njord_mqtt_dedup_total", "{message}");

    internal static readonly Counter<long> MqttPublishFailures =
        Meter.CreateCounter<long>("njord_mqtt_publish_failures_total", "{message}");

    internal static readonly Gauge<double> MqttConnected =
        Meter.CreateGauge<double>("njord_mqtt_connected", "1");
}
