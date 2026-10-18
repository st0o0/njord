using System.Text.Json;
using Njord.Messages.Alerts;
using Njord.Messages.Derived;

namespace Njord.Mqtt.Serialization;

internal abstract class WireNameConverter<TEnum> : WriteOnlyJsonConverter<TEnum>
    where TEnum : struct, Enum
{
    protected abstract string ToWire(TEnum value);

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToWire(value));
}

internal sealed class AlertSeverityWireConverter : WireNameConverter<AlertSeverity>
{
    protected override string ToWire(AlertSeverity value) => value.ToWireName();
}

internal sealed class UvLevelWireConverter : WireNameConverter<UvLevel>
{
    protected override string ToWire(UvLevel value) => value.ToWireName();
}

internal sealed class ExposureRiskWireConverter : WireNameConverter<ExposureRisk>
{
    protected override string ToWire(ExposureRisk value) => value.ToWireName();
}

internal sealed class HumidityComfortWireConverter : WireNameConverter<HumidityComfort>
{
    protected override string ToWire(HumidityComfort value) => value.ToWireName();
}

internal sealed class DewPointComfortWireConverter : WireNameConverter<DewPointComfort>
{
    protected override string ToWire(DewPointComfort value) => value.ToWireName();
}
