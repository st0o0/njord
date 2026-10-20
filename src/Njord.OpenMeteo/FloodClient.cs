using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class FloodClient(HttpClient httpClient) : IFloodClient
{
    internal const string DailyVariables =
        "river_discharge,river_discharge_mean,river_discharge_median,river_discharge_max,river_discharge_min,river_discharge_p25,river_discharge_p75";

    internal const int ForecastDays = 92;

    public async Task<FloodResponse?> FetchAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/flood")
            .WithLocation(latitude, longitude)
            .WithDaily(DailyVariables.Split(','))
            .WithForecastDays(ForecastDays)
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
