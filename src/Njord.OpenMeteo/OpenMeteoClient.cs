using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Forecast;
using Njord.Model;
using Njord.OpenMeteo.Mapping;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.OpenMeteo;

public sealed class OpenMeteoClient(
    HttpClient httpClient,
    ResolvedParameterSet parameters,
    IOptions<NjordOptions> options) : IOpenMeteoClient
{

    private readonly string _hourlyVariables = string.Join(",", parameters.Hourly.Select(p => p.ApiName));
    private readonly string _dailyVariables = string.Join(",", parameters.Daily.Select(p => p.ApiName));
    private readonly int _forecastDays = options.Value.ForecastDays;

    // Ensemble-specific hourly variables (same as regular but API might support _mean/_std suffixes)
    private readonly string _ensembleHourlyVariables = string.Join(",", parameters.Hourly.Select(p => p.ApiName));

    public async Task<PollOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
    {
        var uri = location.ToRequestUri(model, _forecastDays, _hourlyVariables, _dailyVariables);
        var sw = Stopwatch.StartNew();
        PollOutcome? outcome = null;

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                outcome = response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => new PollOutcome.Failure(location.Name, model, PollFailureReason.RateLimited, "HTTP 429 from the forecast endpoint"),
                    HttpStatusCode.BadRequest => new PollOutcome.Failure(location.Name, model, PollFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => new PollOutcome.Failure(location.Name, model, PollFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the forecast endpoint"),
                };
                return outcome;
            }

            var dto = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoForecastResponse, cancellationToken);
            if (dto?.Hourly is null || dto.Hourly.Time.Count == 0)
            {
                outcome = new PollOutcome.Failure(location.Name, model, PollFailureReason.MalformedPayload, "Response contained no hourly data");
                return outcome;
            }

            if (OpenMeteoUnitCheck.Mismatch(dto.HourlyUnits, parameters) is { } mismatch)
            {
                outcome = new PollOutcome.Failure(location.Name, model, PollFailureReason.MalformedPayload, mismatch);
                return outcome;
            }

            var hourlyResult = dto.Hourly.ToForecastSeries(parameters);
            if (hourlyResult is null)
            {
                outcome = new PollOutcome.Failure(location.Name, model, PollFailureReason.MalformedPayload, "Response carried hourly timestamps but no values");
                return outcome;
            }

            var daily = dto.Daily is { Time.Count: > 0 }
                ? dto.Daily.ToDailySeries(parameters)
                : DailyForecastSeries.Empty;

            outcome = new PollOutcome.Success(new WeatherForecast(
                model, location.Name, cycle,
                hourlyResult, daily));
            return outcome;
        }
        catch (HttpRequestException ex)
        {
            outcome = new PollOutcome.Failure(location.Name, model, PollFailureReason.Transport, ex.Message);
            return outcome;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            outcome = new PollOutcome.Failure(location.Name, model, PollFailureReason.Transport, "Request timed out");
            return outcome;
        }
        catch (JsonException)
        {
            outcome = new PollOutcome.Failure(location.Name, model, PollFailureReason.MalformedPayload, "Response JSON did not match the expected schema");
            return outcome;
        }
        finally
        {
            if (outcome is not null)
            {
                sw.Stop();
                var locationTag = new KeyValuePair<string, object?>("location", location.Name);
                var modelTag = new KeyValuePair<string, object?>("model", model.Id);
                var outcomeLabel = FetchOutcomeTag.For(outcome);
                Telemetry.FetchTotal.Add(1, locationTag, modelTag, new KeyValuePair<string, object?>("outcome", outcomeLabel));
                if (outcome is PollOutcome.Success)
                {
                    Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, locationTag, modelTag);
                }
            }
        }
    }

    public async Task<EnsembleFetchResult> FetchEnsembleAsync(LocationOptions location, int forecastDays, CycleId cycle, CancellationToken cancellationToken)
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

            var modelRun = cycle.Timestamp;
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
