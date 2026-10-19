using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Njord.Messages.Forecast;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo;

public sealed class OpenMeteoClient(HttpClient httpClient) : IOpenMeteoClient
{
    public async Task<OpenMeteoFetchResult> FetchAsync(OpenMeteoRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var outcome = await GetAsync(request, cancellationToken);
        sw.Stop();

        var succeeded = outcome is OpenMeteoFetchResult.Success;
        var location = new KeyValuePair<string, object?>("location", request.Location);
        var domain = new KeyValuePair<string, object?>("domain", request.Domain.ToTelemetryTag());
        Telemetry.FetchTotal.Add(1, location, domain, new KeyValuePair<string, object?>("outcome", succeeded ? "success" : "failure"));
        if (succeeded)
        {
            Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, location, domain);
        }

        return outcome;
    }

    private async Task<OpenMeteoFetchResult> GetAsync(OpenMeteoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(request.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => Fail(PollFailureReason.RateLimited, $"HTTP 429 from the {request.Domain.ToTelemetryTag()} endpoint"),
                    HttpStatusCode.BadRequest => Fail(PollFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => Fail(PollFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the {request.Domain.ToTelemetryTag()} endpoint"),
                };
            }

            var body = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoResponse, cancellationToken);
            return body is null
                ? Fail(PollFailureReason.MalformedPayload, "Response contained no data")
                : new OpenMeteoFetchResult.Success(body);
        }
        catch (HttpRequestException ex)
        {
            return Fail(PollFailureReason.Transport, ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(PollFailureReason.Transport, "Request timed out");
        }
        catch (JsonException)
        {
            return Fail(PollFailureReason.MalformedPayload, "Response JSON did not match the expected schema");
        }
    }

    private static OpenMeteoFetchResult.Failure Fail(PollFailureReason reason, string? detail) => new(reason, detail);

    private static async Task<string> ReadErrorReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoErrorResponse, cancellationToken);
            return error?.Reason is { Length: > 0 } reason
                ? $"HTTP 400: {reason}"
                : "HTTP 400 from the forecast endpoint";
        }
        catch (JsonException)
        {
            return "HTTP 400 from the forecast endpoint";
        }
    }
}
