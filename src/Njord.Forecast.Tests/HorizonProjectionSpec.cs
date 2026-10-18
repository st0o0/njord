using WeatherForecast = Njord.Forecast.Forecast;
using Njord.Model;
using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.Forecast.Tests;

public sealed class HorizonProjectionSpec
{
    private static readonly ResolvedParameterSet Parameters = ParameterSelection.Resolve(["Weather"], [], []);
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;
    private static readonly IReadOnlyList<int> Horizons = [3, 6];
    private const int ForecastDays = 2;

    private static WeatherForecast CreateForecast(int hourlyPoints = 96)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = Enumerable.Range(0, hourlyPoints)
            .Select(i => new ForecastPoint(
                Anchor.AddHours(i + 1),
                new Dictionary<ParameterDef, ForecastValue> { [temp] = 20.0 + i }))
            .ToList();

        var tempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
        var daily = new DailyForecastSeries([
            new DailyForecastPoint(DateOnly.FromDateTime(Anchor.UtcDateTime), new Dictionary<ParameterDef, ForecastValue> { [tempMax] = 28.0 }),
            new DailyForecastPoint(DateOnly.FromDateTime(Anchor.AddDays(1).UtcDateTime), new Dictionary<ParameterDef, ForecastValue> { [tempMax] = 26.0 }),
        ]);

        return new WeatherForecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), daily);
    }

    [Fact]
    public void Returns_one_entry_per_horizon_plus_one_per_forecast_day()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor);

        Assert.Equal(Horizons.Count + ForecastDays, result.Count);
        Assert.True(result.ContainsKey(Horizon.Hours(3)));
        Assert.True(result.ContainsKey(Horizon.Hours(6)));
        Assert.True(result.ContainsKey(Horizon.Days(0)));
        Assert.True(result.ContainsKey(Horizon.Days(1)));
    }

    [Fact]
    public void Horizon_entries_carry_the_typed_parameter_values()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, Horizons, ForecastDays, Anchor);

        var h3 = Assert.Single(result[Horizon.Hours(3)]);
        var d0 = Assert.Single(result[Horizon.Days(0)]);
        Assert.Multiple(
            () => Assert.Equal(ParameterRegistry.Temperature2m, h3.Parameter),
            () => Assert.Equal(22.0, h3.Value.Number),
            () => Assert.Null(h3.Value.Text),
            () => Assert.Equal(ParameterRegistry.GetByApiName("temperature_2m_max"), d0.Parameter),
            () => Assert.Equal(28.0, d0.Value.Number));
    }

    [Fact]
    public void Missing_horizon_data_point_is_omitted()
    {
        var forecast = CreateForecast(hourlyPoints: 2);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [24], ForecastDays, Anchor);

        Assert.False(result.ContainsKey(Horizon.Hours(24)));
    }

    [Fact]
    public void Short_range_model_excludes_far_horizons()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3, 6, 12, 24, 48, 72], ForecastDays, Anchor, maxForecastHours: 48);

        Assert.True(result.ContainsKey(Horizon.Hours(3)));
        Assert.True(result.ContainsKey(Horizon.Hours(6)));
        Assert.False(result.ContainsKey(Horizon.Hours(72)));
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
    public void Null_parameter_values_are_omitted_from_the_entries()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, ForecastValue> { [temp] = 20.0, [wind] = default }),
        };
        var forecast = new WeatherForecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 0, Anchor);

        var entry = Assert.Single(result[Horizon.Hours(3)]);
        Assert.Equal(ParameterRegistry.Temperature2m, entry.Parameter);
        Assert.Equal(20.0, entry.Value.Number);
    }

    [Fact]
    public void All_null_horizon_is_excluded_entirely()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, ForecastValue> { [temp] = default }),
        };
        var forecast = new WeatherForecast(new WeatherModel("icon_eu"), "home", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 0, Anchor);

        Assert.False(result.ContainsKey(Horizon.Hours(3)));
    }

    [Fact]
    public void Horizon_clamping_also_limits_daily_day_offsets()
    {
        var forecast = CreateForecast();

        var result = HorizonProjection.BuildPerHorizon(forecast, Parameters, [3], 4, Anchor, maxForecastHours: 48);

        Assert.True(result.ContainsKey(Horizon.Days(0)));
        Assert.True(result.ContainsKey(Horizon.Days(1)));
        Assert.False(result.ContainsKey(Horizon.Days(2)));
        Assert.False(result.ContainsKey(Horizon.Days(3)));
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
    public void ToHourlyValues_returns_the_available_parameters_in_resolved_order()
    {
        var forecast = CreateForecast();
        var point = forecast.Hourly.Points[0];

        var values = HorizonProjection.ToHourlyValues(point, Parameters);

        var value = Assert.Single(values);
        Assert.Multiple(
            () => Assert.Equal(ParameterRegistry.Temperature2m, value.Parameter),
            () => Assert.Equal(20.0, value.Value.Number),
            () => Assert.Null(value.Value.Text));
    }

    [Fact]
    public void ToHourlyValues_is_empty_when_no_parameter_has_a_value()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var point = new ForecastPoint(Anchor, new Dictionary<ParameterDef, ForecastValue> { [temp] = default });

        Assert.Empty(HorizonProjection.ToHourlyValues(point, Parameters));
    }

    [Fact]
    public void ToDailyValues_returns_numeric_and_text_daily_parameters()
    {
        var tempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
        var sunrise = ParameterRegistry.GetByApiName("sunrise")!;
        var point = new DailyForecastPoint(
            DateOnly.FromDateTime(Anchor.UtcDateTime),
            new Dictionary<ParameterDef, ForecastValue> { [tempMax] = 28.0, [sunrise] = new ForecastValue(null, "05:31") });

        var values = HorizonProjection.ToDailyValues(point, Parameters);

        Assert.Equal(2, values.Count);
        var number = Assert.Single(values, v => v.Parameter == tempMax);
        var text = Assert.Single(values, v => v.Parameter == sunrise);
        Assert.Multiple(
            () => Assert.Equal(28.0, number.Value.Number),
            () => Assert.Null(number.Value.Text),
            () => Assert.Null(text.Value.Number),
            () => Assert.Equal("05:31", text.Value.Text));
    }

    [Fact]
    public void ToDailyValues_is_empty_for_empty_daily_point()
    {
        var point = new DailyForecastPoint(
            DateOnly.FromDateTime(Anchor.UtcDateTime),
            new Dictionary<ParameterDef, ForecastValue>());

        Assert.Empty(HorizonProjection.ToDailyValues(point, Parameters));
    }
}
