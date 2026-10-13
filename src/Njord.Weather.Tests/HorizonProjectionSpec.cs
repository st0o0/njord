using System.Text.Json.Nodes;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Weather.Tests;

public sealed class HorizonProjectionSpec
{
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;
    private static readonly IReadOnlyList<int> Horizons = [3, 6];
    private const int ForecastDays = 2;

    private static Forecast CreateForecast(int hourlyPoints = 96)
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

        return new Forecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), daily);
    }

    [Fact]
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

    [Fact]
    public void Horizon_entries_contain_valid_json_with_parameter_keys()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor);

        var h3 = JsonNode.Parse(result["h3"])!;
        Assert.NotNull(h3["temperature"]);
    }

    [Fact]
    public void Missing_horizon_data_point_is_omitted()
    {
        var forecast = CreateForecast(hourlyPoints: 2);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [24], ForecastDays, Anchor);

        Assert.False(result.ContainsKey("h24"));
    }

    [Fact]
    public void Short_range_model_excludes_far_horizons()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3, 6, 12, 24, 48, 72], ForecastDays, Anchor, maxForecastHours: 48);

        Assert.True(result.ContainsKey("h3"));
        Assert.True(result.ContainsKey("h6"));
        Assert.False(result.ContainsKey("h72"));
    }

    [Fact]
    public void Long_range_model_includes_all_horizons()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor, maxForecastHours: 240);

        Assert.Equal(Horizons.Count + ForecastDays, result.Count);
    }

    [Fact]
    public void Unknown_model_with_null_max_includes_all_horizons()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor, maxForecastHours: null);

        Assert.Equal(Horizons.Count + ForecastDays, result.Count);
    }

    [Fact]
    public void Null_parameter_keys_are_omitted_from_json()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 20.0, [wind] = null }),
        };
        var forecast = new Forecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 0, Anchor);

        var json = JsonNode.Parse(result["h3"])!;
        Assert.NotNull(json["temperature"]);
        Assert.Null(json["wind_speed"]);
    }

    [Fact]
    public void All_null_horizon_is_excluded_entirely()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = null }),
        };
        var forecast = new Forecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 0, Anchor);

        Assert.False(result.ContainsKey("h3"));
    }

    [Fact]
    public void Horizon_clamping_also_limits_daily_day_offsets()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 4, Anchor, maxForecastHours: 48);

        Assert.True(result.ContainsKey("d0"));
        Assert.True(result.ContainsKey("d1"));
        Assert.False(result.ContainsKey("d2"));
        Assert.False(result.ContainsKey("d3"));
    }

    [Theory]
    [InlineData(4, null, 4)]
    [InlineData(4, 48, 2)]
    [InlineData(4, 49, 3)]
    [InlineData(2, 240, 2)]
    public void EffectiveDays_clamps_forecast_days_to_model_coverage(int forecastDays, int? maxHours, int expected)
    {
        Assert.Equal(expected, HorizonProjection.EffectiveDays(forecastDays, maxHours));
    }

    [Theory]
    [InlineData(48, null, true)]
    [InlineData(48, 48, true)]
    [InlineData(49, 48, false)]
    public void IsHorizonApplicable_respects_max_forecast_hours(int hours, int? maxHours, bool expected)
    {
        Assert.Equal(expected, HorizonProjection.IsHorizonApplicable(hours, maxHours));
    }

    [Fact]
    public void ToHourlyPayload_serializes_available_parameters()
    {
        var forecast = CreateForecast();
        var point = forecast.Hourly.Points[0];

        var payload = HorizonProjection.ToHourlyPayload(point, Parameters);

        Assert.NotNull(payload);
        Assert.Equal(20.0, JsonNode.Parse(payload)!["temperature"]!.GetValue<double>());
    }

    [Fact]
    public void ToHourlyPayload_returns_null_when_no_parameter_has_a_value()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var point = new ForecastPoint(Anchor, new Dictionary<ParameterDef, double?> { [temp] = null });

        Assert.Null(HorizonProjection.ToHourlyPayload(point, Parameters));
    }

    [Fact]
    public void ToDailyPayload_serializes_numeric_daily_parameters()
    {
        var forecast = CreateForecast();
        var point = forecast.Daily.Points[0];

        var payload = HorizonProjection.ToDailyPayload(point, Parameters);

        Assert.NotNull(payload);
        Assert.Equal(28.0, JsonNode.Parse(payload)!["temperature_max"]!.GetValue<double>());
    }

    [Fact]
    public void ToDailyPayload_returns_null_for_empty_daily_point()
    {
        var point = new DailyForecastPoint(
            DateOnly.FromDateTime(Anchor.UtcDateTime),
            new Dictionary<ParameterDef, double?>(),
            new Dictionary<ParameterDef, string?>());

        Assert.Null(HorizonProjection.ToDailyPayload(point, Parameters));
    }
}
