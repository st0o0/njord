namespace Njord.OpenMeteo.Http;

internal static class OpenMeteoEndpoints
{
    public const string ForecastHost = "https://api.open-meteo.com/";
    public const string EnsembleHost = "https://ensemble-api.open-meteo.com/";
    public const string AirQualityHost = "https://air-quality-api.open-meteo.com/";
    public const string FloodHost = "https://flood-api.open-meteo.com/";
    public const string MarineHost = "https://marine-api.open-meteo.com/";
    public const string SeasonalHost = "https://seasonal-api.open-meteo.com/";

    public const string ForecastPath = "v1/forecast";
    public const string EnsemblePath = "v1/ensemble";
    public const string AirQualityPath = "v1/air-quality";
    public const string FloodPath = "v1/flood";
    public const string MarinePath = "v1/marine";
    public const string SeasonalPath = "v1/seasonal";
}
