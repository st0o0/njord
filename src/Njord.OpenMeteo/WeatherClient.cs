using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

public sealed class WeatherClient(HttpClient httpClient, IOptions<WeatherOptions> options) : IWeatherClient
{
    public async Task<WeatherResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/ensemble")
            .WithLocation(latitude, longitude)
            .WithHourly(variables)
            .With("models", options.Value.Model)
            .With("wind_speed_unit", "ms")
            .WithForecastDays(forecastDays)
            .Build();
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
