using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class AirQualityClient(HttpClient httpClient) : IAirQualityClient
{
    public async Task<AirQualityResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/air-quality")
            .WithLocation(latitude, longitude)
            .WithHourly(variables)
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
