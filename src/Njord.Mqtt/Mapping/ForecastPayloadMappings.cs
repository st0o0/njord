using Njord.Forecast;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Mapping;

internal static class ForecastPayloadMappings
{
    public static ForecastHorizonPayload ToPayload(this IReadOnlyList<HorizonValue> values)
        => new([.. values.Select(value => new ForecastHorizonEntryPayload(value.Parameter.ToMqttKey(), value.Value.Number, value.Value.Text))]);
}
