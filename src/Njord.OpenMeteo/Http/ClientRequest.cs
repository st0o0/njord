using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Njord.Core.Forecast;
using Njord.Messages.Fetch;

namespace Njord.OpenMeteo.Http;

internal static class ClientRequest
{
    public const string Hourly = "hourly";
    public const string Daily = "daily";
    public const string Monthly = "monthly";
    public const string Models = "models";
    public const string Latitude = "latitude";
    public const string Longitude = "longitude";
    public const string TimeFormat = "timeformat";
    public const string TimeZone = "timezone";
    public const string UnixTime = "unixtime";
    public const string Utc = "UTC";
    public const string Iso8601 = "iso8601";
    public const string ForecastDays = "forecast_days";
    public const string ForecastHours = "forecast_hours";
    public const string PastHours = "past_hours";
    public const string Tilt = "tilt";
    public const string Azimuth = "azimuth";
    public const string WindSpeedUnit = "wind_speed_unit";
    public const string MetersPerSecond = "ms";
    public const string Ensemble = "ensemble";

    public static string Uri(string endpoint, double latitude, double longitude, params (string Key, string Value)[] query) =>
        QueryHelpers.AddQueryString(endpoint,
        [
            new(Latitude, latitude.ToString(CultureInfo.InvariantCulture)),
            new(Longitude, longitude.ToString(CultureInfo.InvariantCulture)),
            .. query.Select(q => new KeyValuePair<string, string?>(q.Key, q.Value)),
            new(TimeFormat, UnixTime),
            new(TimeZone, Utc),
        ]);

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    // Open-Meteo azimuth: 0 = south; config uses compass degrees (180 = south).
    public static int ToApiAzimuth(int configAzimuth) => configAzimuth - 180;

    public static string List(IEnumerable<string> values) => string.Join(',', values);

    public static Task<FetchResult> ExecuteAsync<TResponse>(
        Func<Task<TResponse?>> request, Func<TResponse, IDomainForecast?> map, CancellationToken cancellationToken)
        where TResponse : class =>
        ExecuteResultAsync(request, response => map(response) is { } forecast ? new FetchSuccess(forecast) : new FetchEmpty(), cancellationToken);

    public static async Task<FetchResult> ExecuteResultAsync<TResponse>(
        Func<Task<TResponse?>> request, Func<TResponse, FetchResult> map, CancellationToken cancellationToken)
        where TResponse : class
    {
        try
        {
            var response = await request();
            return response is null ? new FetchEmpty() : map(response);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return new FetchEmpty();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new FetchError(PollFailureReason.Transport, $"Fetch failed with {ex.GetType().Name}: {ex.Message}");
        }
    }
}
