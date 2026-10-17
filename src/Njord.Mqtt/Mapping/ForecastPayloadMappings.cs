using Njord.Mqtt.Payloads;
using Njord.Weather;

namespace Njord.Mqtt.Mapping;

internal static class ForecastPayloadMappings
{
    public static ForecastHorizonPayload ToPayload(this IReadOnlyList<HorizonValue> values)
        => new([.. values.Select(value => new ForecastHorizonEntryPayload(value.Parameter.JsonKey, value.Number, value.Text))]);
}
