using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.OpenMeteo.Configuration;

namespace Njord.OpenMeteo;

public sealed class WeatherClient(HttpClient httpClient, IOptions<WeatherOptions> options, IOptions<SolarYieldOptions> solar) : IWeatherClient
{
    private const string GlobalTiltedIrradiance = "global_tilted_irradiance";

    public async Task<WeatherResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        var request = OpenMeteoRequestBuilder.To("v1/ensemble")
            .WithLocation(latitude, longitude)
            .WithHourly(variables)
            .With("models", options.Value.Model)
            .With("wind_speed_unit", "ms");
        if (variables.Contains(GlobalTiltedIrradiance))
        {
            // Open-Meteo azimuth: 0 = south; SolarYieldOptions uses compass degrees (180 = south).
            request = request
                .With("tilt", solar.Value.Tilt.ToString(CultureInfo.InvariantCulture))
                .With("azimuth", (solar.Value.Azimuth - 180).ToString(CultureInfo.InvariantCulture));
        }

        var uri = request.WithForecastDays(forecastDays).Build();
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.WeatherResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
