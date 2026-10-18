using System.Diagnostics.Metrics;

namespace Njord.SensorHub;

internal static class Telemetry
{
    public const string MeterName = "Njord.SensorHub";

    internal static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> Readings =
        Meter.CreateCounter<long>("njord_sensor_readings_total", "{reading}");
}
