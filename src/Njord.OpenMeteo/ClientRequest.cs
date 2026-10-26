using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Messages.Forecast;

namespace Njord.OpenMeteo;

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
