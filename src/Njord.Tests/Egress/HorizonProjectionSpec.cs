using System.Text.Json.Nodes;
using Njord.Domain.Weather;
using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class HorizonProjectionSpec
{
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);
    private static readonly DateTimeOffset Anchor = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<int> Horizons = [3, 6];
    private const int ForecastDays = 2;

    private static ModelForecast CreateForecast(int hourlyPoints = 96)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = Enumerable.Range(0, hourlyPoints)
            .Select(i => new ForecastPoint(
                Anchor.AddHours(i + 1),
                new Dictionary<ParameterDef, double?> { [temp] = 20.0 + i }))
            .ToList();

        var daily = new DailyForecastSeries([
            new DailyForecastPoint(DateOnly.FromDateTime(Anchor.UtcDateTime), new Dictionary<ParameterDef, object?>()),
            new DailyForecastPoint(DateOnly.FromDateTime(Anchor.AddDays(1).UtcDateTime), new Dictionary<ParameterDef, object?>()),
        ]);

        return new ModelForecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), daily);
    }

    [Fact(Timeout = 5000)]
    public void Returns_one_entry_per_horizon_plus_one_per_forecast_day()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor);

        Assert.Equal(Horizons.Count + ForecastDays, result.Count);
        Assert.True(result.ContainsKey("h3"));
        Assert.True(result.ContainsKey("h6"));
        Assert.True(result.ContainsKey("d0"));
        Assert.True(result.ContainsKey("d1"));
    }

    [Fact(Timeout = 5000)]
    public void Horizon_entries_contain_valid_json_with_parameter_keys()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor);

        var h3 = JsonNode.Parse(result["h3"])!;
        Assert.NotNull(h3["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void Missing_horizon_data_point_produces_null_values()
    {
        var forecast = CreateForecast(hourlyPoints: 2);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [24], ForecastDays, Anchor);

        var h24 = JsonNode.Parse(result["h24"])!;
        Assert.Null(h24["temperature"]?.GetValue<double?>());
    }
}
