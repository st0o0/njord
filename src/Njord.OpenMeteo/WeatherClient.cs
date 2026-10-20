using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

public sealed class WeatherClient(HttpClient httpClient, IOptions<WeatherOptions> options) : IWeatherClient
{
    public IReadOnlyList<string> HourlyVariables { get; } = WeatherVariables.For(options.Value.IncludeSolar);

    public async Task<WeatherResponse?> FetchAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/ensemble")
            .WithLocation(latitude, longitude)
            .WithHourly(HourlyVariables)
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
