using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;
using Njord.Grpc.Mapping;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ParamFieldNamesTests
{
    [Fact]
    public void Every_forecast_param_has_a_mapping()
    {
        foreach (var value in Enum.GetValues<ForecastParams>())
        {
            if (value == ForecastParams.None) continue;
            Assert.True(ParamFieldNames.Forecast.ContainsKey(value), $"Missing mapping for ForecastParams.{value}");
        }
    }

    [Fact]
    public void Every_ensemble_param_has_a_mapping()
    {
        foreach (var value in Enum.GetValues<EnsembleParams>())
        {
            if (value == EnsembleParams.None) continue;
            Assert.True(ParamFieldNames.Ensemble.ContainsKey(value), $"Missing mapping for EnsembleParams.{value}");
        }
    }

    [Fact]
    public void Every_air_quality_param_has_a_mapping()
    {
        foreach (var value in Enum.GetValues<AirQualityParams>())
        {
            if (value == AirQualityParams.None) continue;
            Assert.True(ParamFieldNames.AirQuality.ContainsKey(value), $"Missing mapping for AirQualityParams.{value}");
        }
    }

    [Fact]
    public void Every_flood_param_has_a_mapping()
    {
        foreach (var value in Enum.GetValues<FloodParams>())
        {
            if (value is FloodParams.None or FloodParams.Ensemble) continue;
            Assert.True(ParamFieldNames.Flood.ContainsKey(value), $"Missing mapping for FloodParams.{value}");
        }
    }

    [Fact]
    public void Every_marine_param_has_a_mapping()
    {
        foreach (var value in Enum.GetValues<MarineParams>())
        {
            if (value == MarineParams.None) continue;
            Assert.True(ParamFieldNames.Marine.ContainsKey(value), $"Missing mapping for MarineParams.{value}");
        }
    }

    [Fact]
    public void Every_seasonal_param_has_a_mapping()
    {
        foreach (var value in Enum.GetValues<SeasonalParams>())
        {
            if (value == SeasonalParams.None) continue;
            Assert.True(ParamFieldNames.Seasonal.ContainsKey(value), $"Missing mapping for SeasonalParams.{value}");
        }
    }

    [Theory]
    [InlineData(AirQualityParams.Pm25, "pm25")]
    [InlineData(AirQualityParams.Pm10, "pm10")]
    [InlineData(AirQualityParams.BirchPollen, "birch_pollen")]
    public void Air_quality_spot_check(AirQualityParams param, string expected)
    {
        Assert.Equal(expected, ParamFieldNames.AirQuality[param]);
    }

    [Theory]
    [InlineData(MarineParams.WavePeriod, "wave_period")]
    [InlineData(MarineParams.WaveHeight, "wave_height")]
    [InlineData(MarineParams.OceanCurrentVelocity, "ocean_current_velocity")]
    public void Marine_spot_check(MarineParams param, string expected)
    {
        Assert.Equal(expected, ParamFieldNames.Marine[param]);
    }

    [Theory]
    [InlineData(ForecastParams.WeatherCode, "weather_code")]
    [InlineData(ForecastParams.Temperature, "temperature")]
    [InlineData(ForecastParams.WetBulbTemperature, "wet_bulb_temperature_2m")]
    public void Forecast_spot_check(ForecastParams param, string expected)
    {
        Assert.Equal(expected, ParamFieldNames.Forecast[param]);
    }

    [Fact]
    public void Resolve_fields_returns_matching_field_names()
    {
        var flags = AirQualityParams.Pm25 | AirQualityParams.Ozone;
        var fields = ParamFieldNames.ResolveFields(flags, ParamFieldNames.AirQuality);

        Assert.Equal(2, fields.Count);
        Assert.Contains("pm25", fields);
        Assert.Contains("ozone", fields);
    }

    [Fact]
    public void Resolve_fields_returns_empty_for_none()
    {
        var fields = ParamFieldNames.ResolveFields(MarineParams.None, ParamFieldNames.Marine);
        Assert.Empty(fields);
    }
}
