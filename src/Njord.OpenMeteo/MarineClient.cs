using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class MarineClient(HttpClient httpClient) : IMarineClient
{
    internal const string HourlyVariables =
        "wave_height,wave_period,wave_direction,ocean_current_velocity,ocean_current_direction,sea_surface_temperature";

    internal const int ForecastDays = 8;

    public async Task<MarineResponse?> FetchAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/marine")
            .WithLocation(latitude, longitude)
            .WithHourly(HourlyVariables.Split(','))
            .WithForecastDays(ForecastDays)
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
