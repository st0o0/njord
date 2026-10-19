using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Njord.Core.Configuration;
using Njord.Messages.Forecast;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo;

public sealed class OpenMeteoClient(
    HttpClient httpClient,
    ResolvedParameterSet parameters,
    TimeProvider? timeProvider = null) : IOpenMeteoClient
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly string _ensembleHourlyVariables = string.Join(",", parameters.Hourly.Select(p => p.ApiName));

    public Task<EnsembleFetchResult> FetchEnsembleAsync(LocationOptions location, int forecastDays, DateTimeOffset modelRun, CancellationToken cancellationToken) =>
        GetAsync<EnsembleFetchResult>(
            location.ToEnsembleRequestUri(forecastDays, _ensembleHourlyVariables),
            new FetchTag(location.Name, "weather", "ensemble"),
            dto => dto.Hourly is null || dto.Hourly.Time.Count == 0
                ? new EnsembleFetchResult.Failure(PollFailureReason.MalformedPayload, "Response contained no hourly data")
                : dto.Hourly.ToWeatherForecast(modelRun, forecastDays) is { } forecast
                    ? new EnsembleFetchResult.Success(forecast)
                    : new EnsembleFetchResult.Failure(PollFailureReason.MalformedPayload, "Response carried hourly timestamps but no values"),
            (reason, detail) => new EnsembleFetchResult.Failure(reason, detail),
            static r => r is EnsembleFetchResult.Success,
            cancellationToken);

    public Task<AirQualityFetchResult> FetchAirQualityAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken) =>
        GetAsync<AirQualityFetchResult>(
            OpenMeteoRequestMappings.ToAirQualityRequestUri(latitude, longitude, forecastDays),
            new FetchTag(Coordinates(latitude, longitude), "air-quality", "air quality"),
            dto => dto.Hourly is null || dto.Hourly.Time.Count == 0
                ? new AirQualityFetchResult.Failure(PollFailureReason.MalformedPayload, "Response contained no hourly data")
                : dto.Hourly.ToAirQualityForecast(_time.GetUtcNow()) is { } forecast
                    ? new AirQualityFetchResult.Success(forecast)
                    : new AirQualityFetchResult.Failure(PollFailureReason.MalformedPayload, "Response carried hourly timestamps but no values"),
            (reason, detail) => new AirQualityFetchResult.Failure(reason, detail),
            static r => r is AirQualityFetchResult.Success,
            cancellationToken);

    public Task<FloodFetchResult> FetchFloodAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken) =>
        GetAsync<FloodFetchResult>(
            OpenMeteoRequestMappings.ToFloodRequestUri(latitude, longitude),
            new FetchTag(Coordinates(latitude, longitude), "flood", "flood"),
            dto => dto.Daily is null || dto.Daily.Time.Count == 0
                ? new FloodFetchResult.Failure(PollFailureReason.MalformedPayload, "Response contained no daily data")
                : dto.Daily.ToFloodForecast(_time.GetUtcNow()) is { } forecast
                    ? new FloodFetchResult.Success(forecast)
                    : new FloodFetchResult.Failure(PollFailureReason.MalformedPayload, "Response carried daily timestamps but no discharge values"),
            (reason, detail) => new FloodFetchResult.Failure(reason, detail),
            static r => r is FloodFetchResult.Success,
            cancellationToken);

    private static string Coordinates(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{latitude},{longitude}");

    // Shared HTTP GET + JSON parse + metrics; each endpoint supplies its URL, its payload mapping and its failure type.
    private async Task<TResult> GetAsync<TResult>(
        string uri,
        FetchTag tag,
        Func<OpenMeteoForecastResponse, TResult> map,
        Func<PollFailureReason, string?, TResult> fail,
        Func<TResult, bool> isSuccess,
        CancellationToken cancellationToken)
        where TResult : class
    {
        var sw = Stopwatch.StartNew();
        TResult? outcome = null;

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                outcome = response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => fail(PollFailureReason.RateLimited, $"HTTP 429 from the {tag.Endpoint} endpoint"),
                    HttpStatusCode.BadRequest => fail(PollFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => fail(PollFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the {tag.Endpoint} endpoint"),
                };
                return outcome;
            }

            var dto = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoForecastResponse, cancellationToken);
            outcome = dto is null
                ? fail(PollFailureReason.MalformedPayload, "Response contained no data")
                : map(dto);
            return outcome;
        }
        catch (HttpRequestException ex)
        {
            return outcome = fail(PollFailureReason.Transport, ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return outcome = fail(PollFailureReason.Transport, "Request timed out");
        }
        catch (JsonException)
        {
            return outcome = fail(PollFailureReason.MalformedPayload, "Response JSON did not match the expected schema");
        }
        finally
        {
            if (outcome is not null)
            {
                sw.Stop();
                var succeeded = isSuccess(outcome);
                var locationTag = new KeyValuePair<string, object?>("location", tag.Location);
                var domainTag = new KeyValuePair<string, object?>("domain", tag.Domain);
                Telemetry.FetchTotal.Add(1, locationTag, domainTag, new KeyValuePair<string, object?>("outcome", succeeded ? "success" : "failure"));
                if (succeeded)
                {
                    Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, locationTag, domainTag);
                }
            }
        }
    }

    private sealed record FetchTag(string Location, string Domain, string Endpoint);

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
