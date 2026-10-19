using Njord.Forecast;
using Njord.Model;
using Njord.Mqtt.Mapping;

namespace Njord.Mqtt.Tests.Mapping;

public sealed class ForecastPayloadMappingsSpec
{
    [Fact]
    public void Entries_use_the_json_key_of_each_parameter_in_the_given_order()
    {
        var sunrise = ParameterRegistry.GetByApiName("sunrise")!;
        IReadOnlyList<HorizonValue> values =
        [
            new(ParameterRegistry.Temperature2m, new ForecastValue(21.5, null)),
            new(sunrise, new ForecastValue(null, "05:31")),
            new(ParameterRegistry.WindSpeed10m, new ForecastValue(3.0, null)),
        ];

        var payload = values.ToPayload();

        Assert.Equal(3, payload.Entries.Count);
        Assert.Multiple(
            () => Assert.Equal(ParameterRegistry.Temperature2m.ToMqttKey(), payload.Entries[0].Key),
            () => Assert.Equal(21.5, payload.Entries[0].Number),
            () => Assert.Null(payload.Entries[0].Text),
            () => Assert.Equal(sunrise.ToMqttKey(), payload.Entries[1].Key),
            () => Assert.Null(payload.Entries[1].Number),
            () => Assert.Equal("05:31", payload.Entries[1].Text),
            () => Assert.Equal(ParameterRegistry.WindSpeed10m.ToMqttKey(), payload.Entries[2].Key),
            () => Assert.Equal(3.0, payload.Entries[2].Number));
    }

    [Fact]
    public void No_values_map_to_no_entries()
    {
        IReadOnlyList<HorizonValue> values = [];

        Assert.Empty(values.ToPayload().Entries);
    }
}
