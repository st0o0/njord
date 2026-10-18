using Njord.Grpc.Mapping;
using Njord.Model;
using Njord.Forecast;

namespace Njord.Grpc.Tests;

public sealed class ForecastProtoMappingsSpec
{
    private static readonly DateTimeOffset ValidAt = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToHourlyForecast_should_map_all_fixed_fields_when_present()
    {
        var values = new Dictionary<ParameterDef, ForecastValue>
        {
            [ParameterRegistry.Temperature2m] = 21.5,
            [ParameterRegistry.ApparentTemperature] = 20.0,
            [ParameterRegistry.Precipitation] = 0.4,
            [ParameterRegistry.RelativeHumidity2m] = 55,
            [ParameterRegistry.WindSpeed10m] = 3.2,
            [ParameterRegistry.WindGusts10m] = 6.1,
            [ParameterRegistry.GetByApiName("wind_direction_10m")!] = 270,
            [ParameterRegistry.CloudCover] = 40,
            [ParameterRegistry.WeatherCode] = 3,
            [ParameterRegistry.IsDay] = 1,
            [ParameterRegistry.GetByApiName("rain")!] = 0.3,
            [ParameterRegistry.PressureMsl] = 1015.2,
        };

        var hourly = ForecastProtoMappings.ToProto(new ForecastPoint(ValidAt, values));

        Assert.Multiple(
            () => Assert.Equal(ValidAt, hourly.ValidAt.ToDateTimeOffset()),
            () => Assert.Equal(21.5, hourly.Temperature),
            () => Assert.Equal(20.0, hourly.ApparentTemperature),
            () => Assert.Equal(0.4, hourly.Precipitation),
            () => Assert.Equal(55, hourly.Humidity),
            () => Assert.Equal(3.2, hourly.WindSpeed),
            () => Assert.Equal(6.1, hourly.WindGusts),
            () => Assert.Equal(270, hourly.WindBearing),
            () => Assert.Equal(40, hourly.CloudCover),
            () => Assert.Equal(3, hourly.WeatherCode),
            () => Assert.True(hourly.IsDay),
            () => Assert.Equal(0.3, hourly.Rain),
            () => Assert.Equal(1015.2, hourly.PressureMsl),
            () => Assert.Empty(hourly.Extra));
    }

    [Fact]
    public void ToHourlyForecast_should_leave_optional_fields_unset_when_absent()
    {
        var hourly = ForecastProtoMappings.ToProto(
            new ForecastPoint(ValidAt, new Dictionary<ParameterDef, ForecastValue>()));

        Assert.Multiple(
            () => Assert.False(hourly.HasTemperature),
            () => Assert.False(hourly.HasWindBearing),
            () => Assert.False(hourly.HasWeatherCode),
            () => Assert.False(hourly.HasRain),
            () => Assert.False(hourly.HasPressureMsl),
            () => Assert.Empty(hourly.Extra));
    }

    [Fact]
    public void ToHourlyForecast_should_put_non_fixed_parameters_into_extra_and_skip_nulls()
    {
        var values = new Dictionary<ParameterDef, ForecastValue>
        {
            [ParameterRegistry.UvIndex] = 5.5,
            [ParameterRegistry.Cape] = default,
        };

        var hourly = ForecastProtoMappings.ToProto(new ForecastPoint(ValidAt, values));

        var extra = Assert.Single(hourly.Extra);
        Assert.Equal(ParameterRegistry.UvIndex.ApiName, extra.Name);
        Assert.Equal(5.5, extra.Numeric);
    }

    [Fact]
    public void ToDailyForecast_should_map_fixed_fields_and_extras()
    {
        var values = new Dictionary<ParameterDef, ForecastValue>
        {
            [ParameterRegistry.Temperature2mMax] = 28.0,
            [ParameterRegistry.Temperature2mMin] = 14.0,
            [ParameterRegistry.PrecipitationSum] = 1.5,
            [ParameterRegistry.WindSpeed10mMax] = 7.0,
            [ParameterRegistry.WindGusts10mMax] = 12.0,
            [ParameterRegistry.DailyWeatherCode] = 61,
            [ParameterRegistry.Sunrise] = new ForecastValue(null, "2026-07-15T05:40"),
            [ParameterRegistry.Sunset] = new ForecastValue(null, "2026-07-15T21:10"),
        };

        var daily = ForecastProtoMappings.ToProto(
            new DailyForecastPoint(new DateOnly(2026, 7, 15), values));

        Assert.Multiple(
            () => Assert.Equal(new DateOnly(2026, 7, 15).ToString("O"), daily.Date),
            () => Assert.Equal(28.0, daily.TemperatureMax),
            () => Assert.Equal(14.0, daily.TemperatureMin),
            () => Assert.Equal(1.5, daily.PrecipitationSum),
            () => Assert.Equal(7.0, daily.WindSpeedMax),
            () => Assert.Equal(12.0, daily.WindGustsMax),
            () => Assert.Equal(61, daily.WeatherCode),
            () => Assert.Equal("2026-07-15T05:40", daily.Sunrise),
            () => Assert.Equal("2026-07-15T21:10", daily.Sunset),
            () => Assert.Empty(daily.Extra));
    }

    [Fact]
    public void ToDailyForecast_should_default_sunrise_sunset_and_leave_numerics_unset_when_absent()
    {
        var daily = ForecastProtoMappings.ToProto(new DailyForecastPoint(
            new DateOnly(2026, 7, 15),
            new Dictionary<ParameterDef, ForecastValue>()));

        Assert.Multiple(
            () => Assert.Equal("", daily.Sunrise),
            () => Assert.Equal("", daily.Sunset),
            () => Assert.False(daily.HasTemperatureMax),
            () => Assert.False(daily.HasWeatherCode),
            () => Assert.Empty(daily.Extra));
    }

    [Fact]
    public void ToDailyForecast_should_put_non_fixed_numeric_and_text_parameters_into_extra()
    {
        var values = new Dictionary<ParameterDef, ForecastValue>
        {
            [ParameterRegistry.UvIndex] = 6.0,
            [ParameterRegistry.Cape] = new ForecastValue(null, "high"),
        };

        var daily = ForecastProtoMappings.ToProto(
            new DailyForecastPoint(new DateOnly(2026, 7, 15), values));

        Assert.Equal(2, daily.Extra.Count);
        var numericExtra = Assert.Single(daily.Extra, e => e.Name == ParameterRegistry.UvIndex.ApiName);
        Assert.Equal(6.0, numericExtra.Numeric);
        var textExtra = Assert.Single(daily.Extra, e => e.Name == ParameterRegistry.Cape.ApiName);
        Assert.Equal("high", textExtra.Text);
    }
}
