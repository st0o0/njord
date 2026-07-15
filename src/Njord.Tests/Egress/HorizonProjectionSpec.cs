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

        var tempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
        var daily = new DailyForecastSeries([
            new DailyForecastPoint(DateOnly.FromDateTime(Anchor.UtcDateTime), new Dictionary<ParameterDef, double?> { [tempMax] = 28.0 }, new Dictionary<ParameterDef, string?>()),
            new DailyForecastPoint(DateOnly.FromDateTime(Anchor.AddDays(1).UtcDateTime), new Dictionary<ParameterDef, double?> { [tempMax] = 26.0 }, new Dictionary<ParameterDef, string?>()),
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
    public void Missing_horizon_data_point_is_omitted()
    {
        var forecast = CreateForecast(hourlyPoints: 2);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [24], ForecastDays, Anchor);

        Assert.False(result.ContainsKey("h24"));
    }

    [Fact(Timeout = 5000)]
    public void Short_range_model_excludes_far_horizons()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3, 6, 12, 24, 48, 72], ForecastDays, Anchor, maxForecastHours: 48);

        Assert.True(result.ContainsKey("h3"));
        Assert.True(result.ContainsKey("h6"));
        Assert.False(result.ContainsKey("h72"));
    }

    [Fact(Timeout = 5000)]
    public void Long_range_model_includes_all_horizons()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor, maxForecastHours: 240);

        Assert.Equal(Horizons.Count + ForecastDays, result.Count);
    }

    [Fact(Timeout = 5000)]
    public void Unknown_model_with_null_max_includes_all_horizons()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor, maxForecastHours: null);

        Assert.Equal(Horizons.Count + ForecastDays, result.Count);
    }

    [Fact(Timeout = 5000)]
    public void Null_parameter_keys_are_omitted_from_json()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 20.0, [wind] = null }),
        };
        var forecast = new ModelForecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 0, Anchor);

        var json = JsonNode.Parse(result["h3"])!;
        Assert.NotNull(json["temperature"]);
        Assert.Null(json["wind_speed"]);
    }

    [Fact(Timeout = 5000)]
    public void All_null_horizon_is_excluded_entirely()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = null }),
        };
        var forecast = new ModelForecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 0, Anchor);

        Assert.False(result.ContainsKey("h3"));
    }

    [Fact(Timeout = 5000)]
    public void Horizon_clamping_also_limits_daily_day_offsets()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 4, Anchor, maxForecastHours: 48);

        Assert.True(result.ContainsKey("d0"));
        Assert.True(result.ContainsKey("d1"));
        Assert.False(result.ContainsKey("d2"));
        Assert.False(result.ContainsKey("d3"));
    }
}
