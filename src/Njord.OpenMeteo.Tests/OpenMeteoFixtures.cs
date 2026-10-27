namespace Njord.OpenMeteo.Tests;

internal static class OpenMeteoFixtures
{
    public const string ForecastHourly =
        """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000,1003600],"temperature_2m":[20.5,null],"apparent_temperature":[18.0,null],"wind_speed_10m":[3.0,null],"precipitation":[0.0,null],"precipitation_probability":[10.0,null],"cloud_cover":[50.0,null],"pressure_msl":[1013.0,null],"weather_code":[3,null],"relative_humidity_2m":[65.0,null],"dew_point_2m":[12.0,null],"uv_index":[3.0,null]}}""";

    public const string ForecastSingleModel =
        """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m":[20.5]}}""";

    public const string EnsembleSpread =
        """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m_spread_ecmwf_ifs025_ensemble_mean":[1.7]}}""";

    public const string ForecastThreeModels =
        """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000,1003600],"temperature_2m_ecmwf_ifs025_ensemble_mean":[20.5,21.0],"temperature_2m_ecmwf_aifs025_ensemble_mean":[19.5,20.0],"temperature_2m_ukmo_global_ensemble_mean_20km":[null,null],"temperature_2m_member01_ecmwf_ifs025_ensemble_mean":[99.0,99.0]}}""";

    public const string EnsembleMultiModel =
        """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000,1003600],"temperature_2m_ecmwf_ifs025_ensemble_mean":[20.5,21.0],"temperature_2m_ecmwf_aifs025_ensemble_mean":[19.5,20.0],"temperature_2m_spread_ecmwf_ifs025_ensemble_mean":[1.5,1.6]}}""";

    public const string FloodDaily =
        """{"latitude":47.0,"longitude":8.0,"daily":{"time":[86400],"river_discharge":[120.0]}}""";

    public const string MarineHourly =
        """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[3600,7200],"wave_height":[0.4,null],"wave_period":[4.0,null],"wave_direction":[270.0,null],"ocean_current_velocity":[0.2,null],"ocean_current_direction":[90.0,null],"sea_surface_temperature":[19.5,null]}}""";

    public const string NoDataReason = """{"error":true,"reason":"No data is available for this location"}""";

    public const string Malformed = """{"latitude":47.0,"hourly":{"time":[1000,""";
}
