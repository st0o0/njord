using System.Globalization;
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
        var uri = BuildUri(latitude, longitude);
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.FloodResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    internal static string BuildUri(double latitude, double longitude) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"v1/flood?latitude={latitude}&longitude={longitude}" +
            $"&daily={DailyVariables}" +
            $"&forecast_days={ForecastDays}&timeformat=unixtime&timezone=UTC");
}
