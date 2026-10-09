namespace Njord.Mqtt.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void Mqtt_instruments_have_expected_names_units_and_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("njord_mqtt_dedup_total", Telemetry.MqttDedup.Name),
            () => Assert.Equal("{message}", Telemetry.MqttDedup.Unit),
            () => Assert.Equal("Njord.Mqtt", Telemetry.MqttDedup.Meter.Name),
            () => Assert.Equal("njord_mqtt_connected", Telemetry.MqttConnected.Name),
            () => Assert.Equal("1", Telemetry.MqttConnected.Unit),
            () => Assert.Equal("Njord.Mqtt", Telemetry.MqttConnected.Meter.Name));
    }
}
