using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class MarineClient(HttpClient httpClient) : IMarineClient
{
    public async Task<MarineResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/marine")
            .WithLocation(latitude, longitude)
            .WithHourly(variables)
            .WithForecastDays(forecastDays)
            .Build();
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.MarineResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
