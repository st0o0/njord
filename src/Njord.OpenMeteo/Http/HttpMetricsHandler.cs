using System.Diagnostics;

namespace Njord.OpenMeteo.Http;

internal sealed class HttpMetricsHandler : DelegatingHandler
{
    private static readonly string[] _statusClasses = ["0xx", "1xx", "2xx", "3xx", "4xx", "5xx"];

    private static string ToStatusClass(int statusCode)
    {
        var index = statusCode / 100;
        return (uint)index < (uint)_statusClasses.Length ? _statusClasses[index] : "unknown";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            Telemetry.HttpRequests.Add(1, new KeyValuePair<string, object?>("status_class", ToStatusClass((int)response.StatusCode)));
            Telemetry.HttpDuration.Record(sw.Elapsed.TotalSeconds);

            return response;
        }
        catch (Exception) when (sw.IsRunning)
        {
            sw.Stop();
            Telemetry.HttpRequests.Add(1, new KeyValuePair<string, object?>("status_class", "error"));
            Telemetry.HttpDuration.Record(sw.Elapsed.TotalSeconds);
            throw;
        }
    }
}
