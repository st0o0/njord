using System.Collections.Frozen;
using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;

namespace Njord.Grpc.Mapping;

public static class ParamFieldNames
{
    public static FrozenDictionary<ForecastParams, string> Forecast { get; } = new Dictionary<ForecastParams, string>
    {
        [ForecastParams.Temperature] = "temperature",
        [ForecastParams.ApparentTemperature] = "apparent_temperature",
        [ForecastParams.WindSpeed] = "wind_speed",
        [ForecastParams.Precipitation] = "precipitation",
        [ForecastParams.PrecipitationProbability] = "precipitation_probability",
        [ForecastParams.CloudCover] = "cloud_cover",
        [ForecastParams.Pressure] = "pressure",
        [ForecastParams.WeatherCode] = "weather_code",
        [ForecastParams.Humidity] = "humidity",
        [ForecastParams.DewPoint] = "dew_point",
        [ForecastParams.UvIndex] = "uv_index",
        [ForecastParams.WindGusts] = "wind_gusts",
        [ForecastParams.Visibility] = "visibility",
        [ForecastParams.Cape] = "cape",
        [ForecastParams.Rain] = "rain",
        [ForecastParams.Snowfall] = "snowfall",
        [ForecastParams.SoilTemperature] = "soil_temperature",
        [ForecastParams.FreezingLevelHeight] = "freezing_level_height",
        [ForecastParams.IsDay] = "is_day",
        [ForecastParams.ShortwaveRadiation] = "shortwave_radiation",
        [ForecastParams.DirectRadiation] = "direct_radiation",
        [ForecastParams.DiffuseRadiation] = "diffuse_radiation",
        [ForecastParams.DirectNormalIrradiance] = "direct_normal_irradiance",
        [ForecastParams.SunshineDuration] = "sunshine_duration",
        [ForecastParams.WindDirection] = "wind_direction_10m",
        [ForecastParams.GlobalTiltedIrradiance] = "global_tilted_irradiance",
        [ForecastParams.SnowDepth] = "snow_depth",
        [ForecastParams.WetBulbTemperature] = "wet_bulb_temperature_2m",
        [ForecastParams.SnowfallHeight] = "snowfall_height",
        [ForecastParams.UvIndexClearSky] = "uv_index_clear_sky",
        [ForecastParams.Showers] = "showers",
        [ForecastParams.SoilMoisture0To1cm] = "soil_moisture_0_to_1cm",
        [ForecastParams.SoilMoisture1To3cm] = "soil_moisture_1_to_3cm",
        [ForecastParams.Evapotranspiration] = "evapotranspiration",
        [ForecastParams.VapourPressureDeficit] = "vapour_pressure_deficit",
        [ForecastParams.BoundaryLayerHeight] = "boundary_layer_height",
        [ForecastParams.CloudCoverLow] = "cloud_cover_low",
        [ForecastParams.CloudCoverMid] = "cloud_cover_mid",
        [ForecastParams.CloudCoverHigh] = "cloud_cover_high",
        [ForecastParams.ConvectiveInhibition] = "convective_inhibition",
        [ForecastParams.LiftedIndex] = "lifted_index",
        [ForecastParams.SurfacePressure] = "surface_pressure",
        [ForecastParams.WindSpeed80m] = "wind_speed_80m",
        [ForecastParams.WindDirection80m] = "wind_direction_80m",
        [ForecastParams.WindSpeed120m] = "wind_speed_120m",
        [ForecastParams.WindDirection120m] = "wind_direction_120m",
        [ForecastParams.Temperature80m] = "temperature_80m",
        [ForecastParams.Temperature120m] = "temperature_120m",
        [ForecastParams.WindSpeed100m] = "wind_speed_100m",
        [ForecastParams.WindDirection100m] = "wind_direction_100m",
        [ForecastParams.ThunderstormProbability] = "thunderstorm_probability",
        [ForecastParams.PrecipitationType] = "precipitation_type",
        [ForecastParams.SurfaceTemperature] = "surface_temperature",
        [ForecastParams.SnowfallWaterEquivalent] = "snowfall_water_equivalent",
    }.ToFrozenDictionary();

    public static FrozenDictionary<EnsembleParams, string> Ensemble { get; } = new Dictionary<EnsembleParams, string>
    {
        [EnsembleParams.TemperatureSpread] = "temperature_spread",
        [EnsembleParams.PrecipitationSpread] = "precipitation_spread",
        [EnsembleParams.WindSpeedSpread] = "wind_speed_spread",
        [EnsembleParams.PressureMslSpread] = "pressure_msl_spread",
        [EnsembleParams.CloudCoverSpread] = "cloud_cover_spread",
        [EnsembleParams.WindGustsSpread] = "wind_gusts_10m_spread",
        [EnsembleParams.CapeSpread] = "cape_spread",
        [EnsembleParams.SnowfallSpread] = "snowfall_spread",
        [EnsembleParams.RainSpread] = "rain_spread",
        [EnsembleParams.DewPointSpread] = "dew_point_2m_spread",
        [EnsembleParams.HumiditySpread] = "relative_humidity_2m_spread",
        [EnsembleParams.SurfacePressureSpread] = "surface_pressure_spread",
        [EnsembleParams.WindDirectionSpread] = "wind_direction_10m_spread",
        [EnsembleParams.WetBulbTemperatureSpread] = "wet_bulb_temperature_2m_spread",
        [EnsembleParams.WindSpeed100mSpread] = "wind_speed_100m_spread",
    }.ToFrozenDictionary();

    public static FrozenDictionary<AirQualityParams, string> AirQuality { get; } = new Dictionary<AirQualityParams, string>
    {
        [AirQualityParams.Pm25] = "pm25",
        [AirQualityParams.Pm10] = "pm10",
        [AirQualityParams.Ozone] = "ozone",
        [AirQualityParams.NitrogenDioxide] = "nitrogen_dioxide",
        [AirQualityParams.EuropeanAqi] = "european_aqi",
        [AirQualityParams.BirchPollen] = "birch_pollen",
        [AirQualityParams.GrassPollen] = "grass_pollen",
        [AirQualityParams.Alder] = "alder",
        [AirQualityParams.Mugwort] = "mugwort",
        [AirQualityParams.CarbonMonoxide] = "carbon_monoxide",
        [AirQualityParams.SulphurDioxide] = "sulphur_dioxide",
        [AirQualityParams.Dust] = "dust",
        [AirQualityParams.Ammonia] = "ammonia",
        [AirQualityParams.OlivePollen] = "olive_pollen",
        [AirQualityParams.UsAqi] = "us_aqi",
        [AirQualityParams.UvIndex] = "uv_index",
        [AirQualityParams.UvIndexClearSky] = "uv_index_clear_sky",
        [AirQualityParams.RagweedPollen] = "ragweed_pollen",
        [AirQualityParams.EuropeanAqiPm25] = "european_aqi_pm25",
        [AirQualityParams.EuropeanAqiPm10] = "european_aqi_pm10",
        [AirQualityParams.EuropeanAqiNitrogenDioxide] = "european_aqi_nitrogen_dioxide",
        [AirQualityParams.EuropeanAqiOzone] = "european_aqi_ozone",
        [AirQualityParams.EuropeanAqiSulphurDioxide] = "european_aqi_sulphur_dioxide",
        [AirQualityParams.AerosolOpticalDepth] = "aerosol_optical_depth",
        [AirQualityParams.CarbonDioxide] = "carbon_dioxide",
        [AirQualityParams.Methane] = "methane",
        [AirQualityParams.UsAqiPm25] = "us_aqi_pm25",
        [AirQualityParams.UsAqiPm10] = "us_aqi_pm10",
        [AirQualityParams.UsAqiNitrogenDioxide] = "us_aqi_nitrogen_dioxide",
        [AirQualityParams.UsAqiOzone] = "us_aqi_ozone",
        [AirQualityParams.UsAqiSulphurDioxide] = "us_aqi_sulphur_dioxide",
        [AirQualityParams.UsAqiCarbonMonoxide] = "us_aqi_carbon_monoxide",
    }.ToFrozenDictionary();

    public static FrozenDictionary<FloodParams, string> Flood { get; } = new Dictionary<FloodParams, string>
    {
        [FloodParams.Discharge] = "discharge",
        [FloodParams.DischargeMean] = "discharge_mean",
        [FloodParams.DischargeMedian] = "discharge_median",
        [FloodParams.DischargeMax] = "discharge_max",
        [FloodParams.DischargeMin] = "discharge_min",
        [FloodParams.DischargeP25] = "discharge_p25",
        [FloodParams.DischargeP75] = "discharge_p75",
    }.ToFrozenDictionary();

    public static FrozenDictionary<MarineParams, string> Marine { get; } = new Dictionary<MarineParams, string>
    {
        [MarineParams.WaveHeight] = "wave_height",
        [MarineParams.WavePeriod] = "wave_period",
        [MarineParams.WaveDirection] = "wave_direction",
        [MarineParams.WaterTemperature] = "water_temperature",
        [MarineParams.WindWaveHeight] = "wind_wave_height",
        [MarineParams.WindWavePeriod] = "wind_wave_period",
        [MarineParams.WindWaveDirection] = "wind_wave_direction",
        [MarineParams.WindWavePeakPeriod] = "wind_wave_peak_period",
        [MarineParams.SwellWaveHeight] = "swell_wave_height",
        [MarineParams.SwellWavePeriod] = "swell_wave_period",
        [MarineParams.SwellWaveDirection] = "swell_wave_direction",
        [MarineParams.SwellWavePeakPeriod] = "swell_wave_peak_period",
        [MarineParams.SeaLevelHeight] = "sea_level_height_msl",
        [MarineParams.OceanCurrentVelocity] = "ocean_current_velocity",
        [MarineParams.OceanCurrentDirection] = "ocean_current_direction",
        [MarineParams.SecondarySwellWaveHeight] = "secondary_swell_wave_height",
        [MarineParams.SecondarySwellWaveDirection] = "secondary_swell_wave_direction",
        [MarineParams.SecondarySwellWavePeriod] = "secondary_swell_wave_period",
        [MarineParams.TertiarySwellWaveHeight] = "tertiary_swell_wave_height",
        [MarineParams.TertiarySwellWaveDirection] = "tertiary_swell_wave_direction",
        [MarineParams.TertiarySwellWavePeriod] = "tertiary_swell_wave_period",
        [MarineParams.InvertBarometerHeight] = "invert_barometer_height",
        [MarineParams.WavePeakPeriod] = "wave_peak_period",
    }.ToFrozenDictionary();

    public static FrozenDictionary<SeasonalParams, string> Seasonal { get; } = new Dictionary<SeasonalParams, string>
    {
        [SeasonalParams.TemperatureAnomaly] = "temperature_anomaly",
        [SeasonalParams.PrecipitationAnomaly] = "precipitation_anomaly",
        [SeasonalParams.PrecipitationMean] = "precipitation_mean",
    }.ToFrozenDictionary();

    public static IReadOnlyList<string> ResolveFields<TParams>(TParams flags, FrozenDictionary<TParams, string> mapping)
        where TParams : struct, Enum
    {
        var result = new List<string>();
        foreach (var (param, name) in mapping)
        {
            if (flags.HasFlag(param))
            {
                result.Add(name);
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
