using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Njord.Core.Configuration;
using Njord.Model;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo;

public sealed class OpenMeteoClient(
    HttpClient httpClient,
    ResolvedParameterSet parameters,
    TimeProvider? timeProvider = null) : IOpenMeteoClient
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly string _ensembleHourlyVariables = string.Join(",", parameters.Hourly.Select(p => p.ApiName));

    public async Task<EnsembleFetchResult> FetchEnsembleAsync(LocationOptions location, int forecastDays, DateTimeOffset modelRun, CancellationToken cancellationToken)
    {
        var uri = location.ToEnsembleRequestUri(forecastDays, _ensembleHourlyVariables);
        var sw = Stopwatch.StartNew();
        EnsembleFetchResult? outcome = null;

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                outcome = response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => new EnsembleFetchResult.Failure(PollFailureReason.RateLimited, "HTTP 429 from the ensemble endpoint"),
                    HttpStatusCode.BadRequest => new EnsembleFetchResult.Failure(PollFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => new EnsembleFetchResult.Failure(PollFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the ensemble endpoint"),
                };
                return outcome;
            }

            var dto = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoForecastResponse, cancellationToken);
            if (dto?.Hourly is null || dto.Hourly.Time.Count == 0)
            {
                outcome = new EnsembleFetchResult.Failure(PollFailureReason.MalformedPayload, "Response contained no hourly data");
                return outcome;
            }

            var forecast = dto.Hourly.ToWeatherForecast(modelRun, forecastDays);
            if (forecast is null)
            {
                outcome = new EnsembleFetchResult.Failure(PollFailureReason.MalformedPayload, "Response carried hourly timestamps but no values");
                return outcome;
            }

            outcome = new EnsembleFetchResult.Success(forecast);
            return outcome;
        }
        catch (HttpRequestException ex)
        {
            outcome = new EnsembleFetchResult.Failure(PollFailureReason.Transport, ex.Message);
            return outcome;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            outcome = new EnsembleFetchResult.Failure(PollFailureReason.Transport, "Request timed out");
            return outcome;
        }
        catch (JsonException)
        {
            outcome = new EnsembleFetchResult.Failure(PollFailureReason.MalformedPayload, "Response JSON did not match the expected schema");
            return outcome;
        }
        finally
        {
            if (outcome is not null)
            {
                sw.Stop();
                var locationTag = new KeyValuePair<string, object?>("location", location.Name);
                var domainTag = new KeyValuePair<string, object?>("domain", "weather");
                var outcomeLabel = outcome is EnsembleFetchResult.Success ? "success" : "failure";
                Telemetry.FetchTotal.Add(1, locationTag, domainTag, new KeyValuePair<string, object?>("outcome", outcomeLabel));
                if (outcome is EnsembleFetchResult.Success)
                {
                    Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, locationTag, domainTag);
                }
            }
        }
    }

    public async Task<AirQualityFetchResult> FetchAirQualityAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestMappings.ToAirQualityRequestUri(latitude, longitude, forecastDays);
        var sw = Stopwatch.StartNew();
        AirQualityFetchResult? outcome = null;

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                outcome = response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => new AirQualityFetchResult.Failure(PollFailureReason.RateLimited, "HTTP 429 from the air quality endpoint"),
                    HttpStatusCode.BadRequest => new AirQualityFetchResult.Failure(PollFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => new AirQualityFetchResult.Failure(PollFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the air quality endpoint"),
                };
                return outcome;
            }

            var dto = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoForecastResponse, cancellationToken);
            if (dto?.Hourly is null || dto.Hourly.Time.Count == 0)
            {
                outcome = new AirQualityFetchResult.Failure(PollFailureReason.MalformedPayload, "Response contained no hourly data");
                return outcome;
            }

            var forecast = dto.Hourly.ToAirQualityForecast(_time.GetUtcNow());
            outcome = forecast is null
                ? new AirQualityFetchResult.Failure(PollFailureReason.MalformedPayload, "Response carried hourly timestamps but no values")
                : new AirQualityFetchResult.Success(forecast);
            return outcome;
        }
        catch (HttpRequestException ex)
        {
            outcome = new AirQualityFetchResult.Failure(PollFailureReason.Transport, ex.Message);
            return outcome;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            outcome = new AirQualityFetchResult.Failure(PollFailureReason.Transport, "Request timed out");
            return outcome;
        }
        catch (JsonException)
        {
            outcome = new AirQualityFetchResult.Failure(PollFailureReason.MalformedPayload, "Response JSON did not match the expected schema");
            return outcome;
        }
        finally
        {
            if (outcome is not null)
            {
                sw.Stop();
                var coordinates = string.Create(CultureInfo.InvariantCulture, $"{latitude},{longitude}");
                var locationTag = new KeyValuePair<string, object?>("location", coordinates);
                var domainTag = new KeyValuePair<string, object?>("domain", "air-quality");
                var outcomeLabel = outcome is AirQualityFetchResult.Success ? "success" : "failure";
                Telemetry.FetchTotal.Add(1, locationTag, domainTag, new KeyValuePair<string, object?>("outcome", outcomeLabel));
                if (outcome is AirQualityFetchResult.Success)
                {
                    Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, locationTag, domainTag);
                }
            }
        }
    }

    public async Task<FloodFetchResult> FetchFloodAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestMappings.ToFloodRequestUri(latitude, longitude);
        var sw = Stopwatch.StartNew();
        FloodFetchResult? outcome = null;

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                outcome = response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => new FloodFetchResult.Failure(PollFailureReason.RateLimited, "HTTP 429 from the flood endpoint"),
                    HttpStatusCode.BadRequest => new FloodFetchResult.Failure(PollFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => new FloodFetchResult.Failure(PollFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the flood endpoint"),
                };
                return outcome;
            }

            var dto = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoForecastResponse, cancellationToken);
            if (dto?.Daily is null || dto.Daily.Time.Count == 0)
            {
                outcome = new FloodFetchResult.Failure(PollFailureReason.MalformedPayload, "Response contained no daily data");
                return outcome;
            }

            var forecast = dto.Daily.ToFloodForecast(_time.GetUtcNow());
            outcome = forecast is null
                ? new FloodFetchResult.Failure(PollFailureReason.MalformedPayload, "Response carried daily timestamps but no discharge values")
                : new FloodFetchResult.Success(forecast);
            return outcome;
        }
        catch (HttpRequestException ex)
        {
            outcome = new FloodFetchResult.Failure(PollFailureReason.Transport, ex.Message);
            return outcome;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            outcome = new FloodFetchResult.Failure(PollFailureReason.Transport, "Request timed out");
            return outcome;
        }
        catch (JsonException)
        {
            outcome = new FloodFetchResult.Failure(PollFailureReason.MalformedPayload, "Response JSON did not match the expected schema");
            return outcome;
        }
        finally
        {
            if (outcome is not null)
            {
                sw.Stop();
                var coordinates = string.Create(CultureInfo.InvariantCulture, $"{latitude},{longitude}");
                var locationTag = new KeyValuePair<string, object?>("location", coordinates);
                var domainTag = new KeyValuePair<string, object?>("domain", "flood");
                var outcomeLabel = outcome is FloodFetchResult.Success ? "success" : "failure";
                Telemetry.FetchTotal.Add(1, locationTag, domainTag, new KeyValuePair<string, object?>("outcome", outcomeLabel));
                if (outcome is FloodFetchResult.Success)
                {
                    Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, locationTag, domainTag);
                }
            }
        }
    }

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
