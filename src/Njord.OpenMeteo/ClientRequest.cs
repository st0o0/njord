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
    public static string Uri(string endpoint, double latitude, double longitude, params (string Key, string Value)[] query) =>
        QueryHelpers.AddQueryString(endpoint,
        [
            new("latitude", latitude.ToString(CultureInfo.InvariantCulture)),
            new("longitude", longitude.ToString(CultureInfo.InvariantCulture)),
            .. query.Select(q => new KeyValuePair<string, string?>(q.Key, q.Value)),
            new("timeformat", "unixtime"),
            new("timezone", "UTC"),
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
