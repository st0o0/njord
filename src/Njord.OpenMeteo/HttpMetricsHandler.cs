using System.Diagnostics;

namespace Njord.OpenMeteo;

internal sealed class HttpMetricsHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            sw.Stop();

            var statusClass = (int)response.StatusCode / 100 + "xx";
            Telemetry.HttpRequests.Add(1, new KeyValuePair<string, object?>("status_class", statusClass));
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
