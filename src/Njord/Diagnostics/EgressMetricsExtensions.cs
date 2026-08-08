using System.Diagnostics.Metrics;

namespace Njord.Diagnostics;

public static class EgressMetricsExtensions
{
    public static Counter<long> AddMqttDedup(this NjordMetrics m) =>
        m.Meter.CreateCounter<long>("njord_mqtt_dedup_total", "{message}");

    public static Gauge<double> AddMqttConnected(this NjordMetrics m) =>
        m.Meter.CreateGauge<double>("njord_mqtt_connected", "1");
}
