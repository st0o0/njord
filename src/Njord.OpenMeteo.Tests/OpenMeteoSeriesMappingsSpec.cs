using Njord.OpenMeteo.Mapping;
using Njord.Model;
using Njord.Forecast;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoSeriesMappingsSpec
{
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef Sunrise = ParameterRegistry.GetByApiName("sunrise")!;

    private static readonly ResolvedParameterSet Parameters = new([Temperature, WindSpeed], [TempMax, Sunrise]);

    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_783_728_000);

    private static OpenMeteoTimeSeries Series(string json)
    {
        var response = System.Text.Json.JsonSerializer.Deserialize(
            $$"""{ "hourly": {{json}}, "daily": {{json}} }""",
            OpenMeteoJsonContext.Default.OpenMeteoForecastResponse);
        return Assert.IsType<OpenMeteoForecastResponse>(response).Hourly!;
    }

    [Fact]
    public void Hourly_values_are_mapped_per_parameter_and_time()
    {
        var series = Series("""{ "time": [1783728000, 1783731600], "temperature_2m": [20.2, 21.5], "wind_speed_10m": [3.4, 4.1] }""");

        var mapped = series.ToForecastSeries(Parameters);

        Assert.NotNull(mapped);
        Assert.Equal(2, mapped.Points.Count);
        Assert.Equal(Start, mapped.Points[0].ValidAt);
        Assert.Equal(Start.AddHours(1), mapped.Points[1].ValidAt);
        Assert.Equal(20.2, mapped.Points[0].Get(Temperature).Number);
        Assert.Equal(3.4, mapped.Points[0].Get(WindSpeed).Number);
        Assert.Equal(21.5, mapped.Points[1].Get(Temperature).Number);
        Assert.Equal(4.1, mapped.Points[1].Get(WindSpeed).Number);
    }

    [Fact]
    public void Hourly_nulls_and_missing_variables_map_to_null_values()
    {
        var series = Series("""{ "time": [1783728000, 1783731600], "temperature_2m": [20.2, null] }""");

        var mapped = series.ToForecastSeries(Parameters);

        Assert.NotNull(mapped);
        Assert.Single(mapped.Points);
        Assert.Equal(20.2, mapped.Points[0].Get(Temperature).Number);
        Assert.False(mapped.Points[0].Get(WindSpeed).HasValue);
    }

    [Fact]
    public void Trailing_all_null_hours_are_trimmed_but_inner_gaps_stay()
    {
        var series = Series("""{ "time": [1783728000, 1783731600, 1783735200, 1783738800], "temperature_2m": [1.0, null, 3.0, null] }""");

        var mapped = series.ToForecastSeries(Parameters);

        Assert.NotNull(mapped);
        Assert.Equal(3, mapped.Points.Count);
        Assert.False(mapped.Points[1].Get(Temperature).HasValue);
        Assert.Equal(3.0, mapped.Points[2].Get(Temperature).Number);
    }

    [Fact]
    public void A_series_without_any_value_maps_to_null()
    {
        var series = Series("""{ "time": [1783728000, 1783731600], "temperature_2m": [null, null] }""");

        Assert.Null(series.ToForecastSeries(Parameters));
    }

    [Fact]
    public void Daily_numeric_values_and_string_times_are_mapped()
    {
        var series = Series("""{ "time": [1783728000], "temperature_2m_max": [25.5], "sunrise": ["2026-07-11T05:23"] }""");

        var mapped = series.ToDailySeries(Parameters);

        var point = Assert.Single(mapped.Points);
        Assert.Equal(new DateOnly(2026, 7, 11), point.Date);
        Assert.Equal(25.5, point.Get(TempMax).Number);
        Assert.Equal("2026-07-11T05:23", point.Get(Sunrise).Text);
    }

    [Fact]
    public void Daily_numeric_time_values_are_converted_to_iso_8601()
    {
        var series = Series("""{ "time": [1783728000], "sunrise": [1783746180] }""");

        var mapped = series.ToDailySeries(Parameters);

        var point = Assert.Single(mapped.Points);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783746180).ToString("O"), point.Get(Sunrise).Text);
        Assert.Null(point.Get(TempMax).Number);
    }

    [Fact]
    public void Daily_points_without_any_value_are_dropped()
    {
        var series = Series("""{ "time": [1783728000, 1783814400], "temperature_2m_max": [null, 22.0], "sunrise": [null, null] }""");

        var mapped = series.ToDailySeries(Parameters);

        var point = Assert.Single(mapped.Points);
        Assert.Equal(new DateOnly(2026, 7, 12), point.Date);
        Assert.Equal(22.0, point.Get(TempMax).Number);
    }
}
