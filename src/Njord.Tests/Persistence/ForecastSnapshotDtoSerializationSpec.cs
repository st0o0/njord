using Newtonsoft.Json;
using Njord.Domain.Weather;
using Njord.Persistence;

using static VerifyXunit.Verifier;

namespace Njord.Tests.Persistence;

public sealed class ForecastSnapshotDtoSerializationSpec
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact(Timeout = 5000)]
    public Task ForecastSnapshot_dto_produces_stable_wire_format()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var forecast = new ModelForecast(
            new WeatherModel("icon_d2"), "lucerne", new CycleId(TestTime),
            new ForecastSeries([new ForecastPoint(TestTime.AddHours(3),
                new Dictionary<ParameterDef, double?> { [temp] = 28.8 })]),
            DailyForecastSeries.Empty);

        var state = new Dictionary<string, ModelForecast> { ["lucerne|icon_d2"] = forecast };
        var dto = ForecastSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
        return Verify(json);
    }

    [Fact(Timeout = 5000)]
    public void ForecastSnapshot_dto_round_trips_to_domain()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var forecast = new ModelForecast(
            new WeatherModel("icon_d2"), "lucerne", new CycleId(TestTime),
            new ForecastSeries([new ForecastPoint(TestTime.AddHours(3),
                new Dictionary<ParameterDef, double?> { [temp] = 28.8 })]),
            DailyForecastSeries.Empty);

        var state = new Dictionary<string, ModelForecast> { ["lucerne|icon_d2"] = forecast };
        var dto = ForecastSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<ForecastSnapshotDto>(json)!;
        var result = ForecastSnapshotMapping.ToDomain(deserialized);

        Assert.Single(result);
        Assert.Equal("icon_d2", result["lucerne|icon_d2"].Model.Id);
        Assert.Equal(28.8, result["lucerne|icon_d2"].Hourly.Points[0].Get(temp));
    }
}
