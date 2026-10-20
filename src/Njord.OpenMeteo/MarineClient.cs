using System.Globalization;
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
        var uri = BuildUri(latitude, longitude);
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.MarineResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    internal static string BuildUri(double latitude, double longitude) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"v1/marine?latitude={latitude}&longitude={longitude}" +
            $"&hourly={HourlyVariables}" +
            $"&forecast_days={ForecastDays}&timeformat=unixtime&timezone=UTC");
}
