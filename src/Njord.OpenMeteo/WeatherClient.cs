using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

public sealed class WeatherClient(HttpClient httpClient, ResolvedParameterSet parameters) : IWeatherClient
{
    private readonly string _hourlyVariables = string.Join(",", parameters.Hourly.Select(p => p.ApiName));

    public async Task<WeatherResponse?> FetchAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = BuildUri(latitude, longitude, forecastDays);
        return await FetchCoreAsync<WeatherResponse>(httpClient, uri, OpenMeteoJsonContext.Default.WeatherResponse, cancellationToken);
    }

    private string BuildUri(double latitude, double longitude, int forecastDays) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"v1/ensemble?latitude={latitude}&longitude={longitude}" +
            $"&hourly={_hourlyVariables}" +
            $"&wind_speed_unit=ms&timeformat=unixtime&timezone=UTC&forecast_days={forecastDays}");

    internal static async Task<T?> FetchCoreAsync<T>(
        HttpClient httpClient,
        string uri,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    await LogBadRequestAsync(response, cancellationToken);
                }

                return null;
            }

            return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task LogBadRequestAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            _ = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoErrorResponse, cancellationToken);
        }
        catch (JsonException)
        {
            // Ignore parse failures for error responses.
        }
    }
}
