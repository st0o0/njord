using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.OpenMeteo.AirQuality;
using Njord.OpenMeteo.Flood;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;
using Njord.OpenMeteo.Marine;
using Njord.OpenMeteo.Weather;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoResponseMappingsSpec
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private const long Hour0 = 1_783_771_200;

    [Fact]
    public void A_weather_response_maps_to_hourly_weather_points()
    {
        var response = new WeatherResponse(47.0, 8.0,
            new WeatherHourlyData(
                Time: [Hour0, Hour0 + 3600],
                Temperature: [20.5, 21.0],
                ApparentTemperature: [18.0, 19.0],
                WindSpeed: [3.0, 4.0],
                Precipitation: [0.0, 0.0],
                PrecipitationProbability: [10.0, 20.0],
                CloudCover: [50.0, 60.0],
                Pressure: [1013.0, 1012.0],
                WeatherCode: [3, 61],
                Humidity: [65.0, 70.0],
                DewPoint: [12.0, 13.0],
                UvIndex: [3.0, 2.0],
                WindGusts: [8.0, null]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Weather, forecast.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(Run, forecast.ModelRun),
            () => Assert.Equal(2, forecast.Points.Length),
            () => Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(Hour0), forecast.Points[0].ValidFrom),
            () => Assert.Equal(20.5, forecast.Points[0].Temperature),
            () => Assert.Equal(61.0, forecast.Points[1].WeatherCode),
            () => Assert.Equal(8.0, forecast.Points[0].WindGusts),
            () => Assert.Null(forecast.Points[1].WindGusts));
    }

    [Fact]
    public void A_weather_response_trims_trailing_hours_without_any_value()
    {
        var response = new WeatherResponse(47.0, 8.0,
            new WeatherHourlyData(
                Time: [Hour0, Hour0 + 3600, Hour0 + 7200],
                Temperature: [20.5, 21.0, null],
                WindGusts: [8.0, null, null]));

        var forecast = response.ToDomainForecast(Run)?.TrimTrailing((WeatherPoint p, WeatherParams f) => p.HasFields(f));

        Assert.NotNull(forecast);
        Assert.Equal(
            [DateTimeOffset.FromUnixTimeSeconds(Hour0), DateTimeOffset.FromUnixTimeSeconds(Hour0 + 3600)],
            forecast.Points.Select(p => p.ValidFrom));
    }

    [Fact]
    public void A_weather_time_axis_longer_than_its_data_arrays_trims_the_hours_without_data()
    {
        var response = new WeatherResponse(47.0, 8.0,
            new WeatherHourlyData(
                Time: [Hour0, Hour0 + 3600, Hour0 + 7200, Hour0 + 10_800],
                Temperature: [20.5, 21.0]));

        var forecast = response.ToDomainForecast(Run)?.TrimTrailing((WeatherPoint p, WeatherParams f) => p.HasFields(f));

        Assert.NotNull(forecast);
        Assert.Equal([20.5, 21.0], forecast.Points.Select(p => p.Temperature));
    }

    [Fact]
    public void Mapping_alone_keeps_trailing_hours_without_any_value()
    {
        var response = new WeatherResponse(47.0, 8.0,
            new WeatherHourlyData(Time: [Hour0, Hour0 + 3600], Temperature: [20.5, null]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        Assert.Equal(2, forecast.Points.Length);
    }

    [Fact]
    public void Raw_day_flag_and_weather_code_are_passed_through_unconverted()
    {
        var response = new WeatherResponse(47.0, 8.0,
            new WeatherHourlyData(Time: [Hour0], WeatherCode: [61.0], IsDay: [1.0]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(61.0, point.WeatherCode),
            () => Assert.Equal(1.0, point.IsDay));
    }

    [Fact]
    public void A_weather_response_without_hourly_data_maps_to_nothing()
    {
        Assert.Null(new WeatherResponse(0, 0, null).ToDomainForecast(Run));
    }

    [Fact]
    public void An_air_quality_response_maps_to_hourly_points_with_optional_pollen()
    {
        var response = new AirQualityResponse(47.0, 8.0,
            new AirQualityHourlyData(
                Time: [Hour0, Hour0 + 3600],
                Pm25: [8.0, null],
                Pm10: [15.0, null],
                Ozone: [60.0, null],
                BirchPollen: [12.0, null]));

        var forecast = response.ToDomainForecast(Run)?.TrimTrailing((AirQualityPoint p, AirQualityParams f) => p.HasFields(f));

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.AirQuality, forecast.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(8.0, point.Pm25),
            () => Assert.Equal(60.0, point.Ozone),
            () => Assert.Equal(12.0, point.BirchPollen),
            () => Assert.Null(point.GrassPollen));
    }

    [Fact]
    public void An_air_quality_response_maps_the_expanded_variables_and_keeps_missing_ones_null()
    {
        var json = """
            {"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000,4600],
            "carbon_monoxide":[200.0,null],"sulphur_dioxide":[3.0,null],"dust":[25.0,null],
            "ammonia":[1.5,null],"olive_pollen":[4.0,null],"us_aqi":[42.0,null],
            "uv_index":[5.5,null],"uv_index_clear_sky":[6.5,null]}}
            """;
        var response = System.Text.Json.JsonSerializer.Deserialize<AirQualityResponse>(json);
        Assert.NotNull(response);

        var forecast = response.ToDomainForecast(Run)?.TrimTrailing((AirQualityPoint p, AirQualityParams f) => p.HasFields(f));

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(200.0, point.CarbonMonoxide),
            () => Assert.Equal(3.0, point.SulphurDioxide),
            () => Assert.Equal(25.0, point.Dust),
            () => Assert.Equal(1.5, point.Ammonia),
            () => Assert.Equal(4.0, point.OlivePollen),
            () => Assert.Equal(42.0, point.UsAqi),
            () => Assert.Equal(5.5, point.UvIndex),
            () => Assert.Equal(6.5, point.UvIndexClearSky),
            () => Assert.Null(point.Pm25));
    }

    [Fact]
    public void A_flood_response_maps_to_daily_points_and_skips_days_without_discharge()
    {
        var response = new FloodResponse(47.0, 8.0,
            new FloodDailyData(
                Time: [Hour0, Hour0 + 86_400],
                Discharge: [120.0, null],
                DischargeP75: [140.0, null]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, forecast.Domain),
            () => Assert.Equal(TimeResolution.Daily, forecast.Resolution),
            () => Assert.Equal(120.0, point.Discharge),
            () => Assert.Equal(140.0, point.DischargeP75),
            () => Assert.Null(point.DischargeMedian));
    }

    [Fact]
    public void A_marine_response_maps_to_hourly_points()
    {
        var response = new MarineResponse(47.0, 8.0,
            new MarineHourlyData(
                Time: [Hour0, Hour0 + 3600],
                WaveHeight: [0.4, 1.2],
                WavePeriod: [4.0, 5.0],
                WaveDirection: [270.0, 280.0],
                SeaSurfaceTemperature: [19.0, null]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        Assert.Equal(2, forecast.Points.Length);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Marine, forecast.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(0.4, forecast.Points[0].WaveHeight),
            () => Assert.Equal(4.0, forecast.Points[0].WavePeriod),
            () => Assert.Equal(270.0, forecast.Points[0].WaveDirection),
            () => Assert.Equal(19.0, forecast.Points[0].WaterTemperature),
            () => Assert.Equal(1.2, forecast.Points[1].WaveHeight),
            () => Assert.Null(forecast.Points[1].WaterTemperature));
    }

    [Fact]
    public void A_marine_response_maps_swell_wind_wave_and_sea_level_separately_from_combined_waves()
    {
        var response = new MarineResponse(47.0, 8.0,
            new MarineHourlyData(
                Time: [Hour0, Hour0 + 3600],
                WaveHeight: [1.5, 1.6],
                WindWaveHeight: [0.5, null],
                WindWavePeriod: [4.0, null],
                WindWaveDirection: [90.0, null],
                WindWavePeakPeriod: [5.0, null],
                SwellWaveHeight: [1.4, 1.5],
                SwellWavePeriod: [12.0, 13.0],
                SwellWaveDirection: [270.0, 275.0],
                SwellWavePeakPeriod: [14.0, 15.0],
                SeaLevelHeight: [0.3, 0.4]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        var first = forecast.Points[0];
        Assert.Multiple(
            () => Assert.Equal(1.5, first.WaveHeight),
            () => Assert.Equal(0.5, first.WindWaveHeight),
            () => Assert.Equal(4.0, first.WindWavePeriod),
            () => Assert.Equal(90.0, first.WindWaveDirection),
            () => Assert.Equal(5.0, first.WindWavePeakPeriod),
            () => Assert.Equal(1.4, first.SwellWaveHeight),
            () => Assert.Equal(12.0, first.SwellWavePeriod),
            () => Assert.Equal(270.0, first.SwellWaveDirection),
            () => Assert.Equal(14.0, first.SwellWavePeakPeriod),
            () => Assert.Equal(0.3, first.SeaLevelHeight),
            () => Assert.Null(forecast.Points[1].WindWaveHeight),
            () => Assert.Equal(1.5, forecast.Points[1].SwellWaveHeight));
    }

    [Fact]
    public void A_marine_response_with_only_sea_level_values_still_maps()
    {
        var response = new MarineResponse(47.0, 8.0,
            new MarineHourlyData(Time: [Hour0], SeaLevelHeight: [0.7]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        Assert.Equal(0.7, Assert.Single(forecast.Points).SeaLevelHeight);
    }

    [Fact]
    public void A_marine_response_trims_trailing_hours_without_any_value()
    {
        var response = new MarineResponse(47.0, 8.0,
            new MarineHourlyData(
                Time: [Hour0, Hour0 + 3600, Hour0 + 7200],
                WaveHeight: [0.4, 1.2, null],
                WavePeriod: [4.0, 5.0, null],
                WaveDirection: [270.0, 280.0, null]));

        var forecast = response.ToDomainForecast(Run)?.TrimTrailing((MarinePoint p, MarineParams f) => p.HasFields(f));

        Assert.NotNull(forecast);
        Assert.Equal([0.4, 1.2], forecast.Points.Select(p => p.WaveHeight));
    }

    [Fact]
    public void A_marine_response_without_any_wave_value_maps_to_nothing()
    {
        var response = new MarineResponse(47.0, 8.0,
            new MarineHourlyData(Time: [Hour0], WaveHeight: [null], WavePeriod: [null], WaveDirection: [null]));

        Assert.Null(response.ToDomainForecast(Run)?.TrimTrailing((MarinePoint p, MarineParams f) => p.HasFields(f)));
    }

    [Fact]
    public void A_flood_ensemble_response_maps_members_per_day_and_drops_nulls()
    {
        const string json = """
            {"latitude":47.0,"longitude":8.0,"daily":{"time":[86400,172800],"river_discharge":[120.0,130.0],
            "river_discharge_member01":[100.0,null],"river_discharge_member02":[110.0,150.0],"other":[1,2]}}
            """;
        var response = System.Text.Json.JsonSerializer.Deserialize(json, OpenMeteoJsonContext.Default.FloodResponse);

        var forecast = response!.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        Assert.Equal(2, forecast.Points.Length);
        Assert.Multiple(
            () => Assert.Equal([100.0, 110.0], forecast.Points[0].EnsembleMembers),
            () => Assert.Equal([150.0], forecast.Points[1].EnsembleMembers));
    }

    [Fact]
    public void A_flood_response_without_member_series_has_no_ensemble_members()
    {
        var response = new FloodResponse(47.0, 8.0, new FloodDailyData(Time: [Hour0], Discharge: [120.0]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        Assert.Null(Assert.Single(forecast.Points).EnsembleMembers);
    }
}
