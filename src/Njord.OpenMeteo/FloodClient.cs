using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class FloodClient(HttpClient httpClient) : IFloodClient
{
    public async Task<FloodResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/flood")
            .WithLocation(latitude, longitude)
            .WithDaily(variables)
            .WithForecastDays(forecastDays)
            .Build();
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.FloodResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
