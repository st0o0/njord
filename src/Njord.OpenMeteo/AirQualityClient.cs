using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class AirQualityClient(HttpClient httpClient) : IAirQualityClient
{
    internal const string HourlyVariables =
        "pm2_5,pm10,ozone,nitrogen_dioxide,european_aqi,alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,ragweed_pollen";

    public async Task<AirQualityResponse?> FetchAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = BuildUri(latitude, longitude, forecastDays);
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.AirQualityResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    internal static string BuildUri(double latitude, double longitude, int forecastDays) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"v1/air-quality?latitude={latitude}&longitude={longitude}" +
            $"&hourly={HourlyVariables}" +
            $"&forecast_days={forecastDays}&timeformat=unixtime&timezone=UTC");
}
