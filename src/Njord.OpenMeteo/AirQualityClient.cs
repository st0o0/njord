using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class AirQualityClient(HttpClient httpClient) : IAirQualityClient
{
    internal const string HourlyVariables =
        "pm2_5,pm10,ozone,nitrogen_dioxide,european_aqi,alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,ragweed_pollen";

    public async Task<AirQualityResponse?> FetchAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/air-quality")
            .WithLocation(latitude, longitude)
            .WithHourly(HourlyVariables.Split(','))
            .WithForecastDays(forecastDays)
            .Build();
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.AirQualityResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
