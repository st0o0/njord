using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

public sealed class WeatherClient(HttpClient httpClient, ResolvedParameterSet parameters) : IWeatherClient
{
    private readonly string _hourlyVariables = string.Join(",", parameters.Hourly.Select(p => p.ApiName));

    public async Task<WeatherResponse?> FetchAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = BuildUri(latitude, longitude, forecastDays);
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.WeatherResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    private string BuildUri(double latitude, double longitude, int forecastDays) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"v1/ensemble?latitude={latitude}&longitude={longitude}" +
            $"&hourly={_hourlyVariables}" +
            $"&wind_speed_unit=ms&timeformat=unixtime&timezone=UTC&forecast_days={forecastDays}");
}
