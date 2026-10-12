using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Weather;

namespace Njord.OpenMeteo;

public sealed class OpenMeteoClient(
    HttpClient httpClient,
    ResolvedParameterSet parameters,
    IOptions<NjordOptions> options) : IOpenMeteoClient
{

    private readonly string _hourlyVariables = string.Join(",", parameters.Hourly.Select(p => p.ApiName));
    private readonly string _dailyVariables = string.Join(",", parameters.Daily.Select(p => p.ApiName));
    private readonly int _forecastDays = options.Value.ForecastDays;

    public async Task<PollOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
    {
        var uri = BuildUri(location, model);
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

            if (UnitMismatch(dto.HourlyUnits) is { } mismatch)
            {
                outcome = new PollOutcome.Failure(location.Name, model, PollFailureReason.MalformedPayload, mismatch);
                return outcome;
            }

            var hourlyResult = MapHourly(dto.Hourly);
            if (hourlyResult is null)
            {
                outcome = new PollOutcome.Failure(location.Name, model, PollFailureReason.MalformedPayload, "Response carried hourly timestamps but no values");
                return outcome;
            }

            var daily = dto.Daily is { Time.Count: > 0 }
                ? MapDaily(dto.Daily)
                : DailyForecastSeries.Empty;

            outcome = new PollOutcome.Success(new Forecast(
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
                var outcomeLabel = outcome switch
                {
                    PollOutcome.Success => "success",
                    PollOutcome.Failure f => f.Reason.ToString().ToLowerInvariant(),
                    _ => "unknown",
                };
                Telemetry.FetchTotal.Add(1, locationTag, modelTag, new KeyValuePair<string, object?>("outcome", outcomeLabel));
                if (outcome is PollOutcome.Success)
                {
                    Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, locationTag, modelTag);
                }
            }
        }
    }

    private string BuildUri(LocationOptions location, WeatherModel model)
    {
        var effectiveDays = ModelCoverageRegistry.EffectiveForecastDays(model.Id, _forecastDays);
        var uri = string.Create(
            CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}" +
            $"&models={Uri.EscapeDataString(model.Id)}&hourly={_hourlyVariables}" +
            $"&wind_speed_unit=ms&timeformat=unixtime&timezone=UTC&forecast_days={effectiveDays}");

        if (_dailyVariables.Length > 0)
        {
            uri += $"&daily={_dailyVariables}";
        }

        return uri;
    }

    private ForecastSeries? MapHourly(OpenMeteoTimeSeries series)
    {
        var times = series.Time;
        var paramArrays = new Dictionary<ParameterDef, IReadOnlyList<double?>>(parameters.Hourly.Count);

        foreach (var param in parameters.Hourly)
        {
            paramArrays[param] = series.Variables.TryGetValue(param.ApiName, out var element)
                ? ParseNumberArray(element, times.Count)
                : NullArray(times.Count);
        }

        var points = new List<ForecastPoint>(times.Count);
        for (var i = 0; i < times.Count; i++)
        {
            var dict = new Dictionary<ParameterDef, double?>(parameters.Hourly.Count);
            foreach (var param in parameters.Hourly)
            {
                dict[param] = paramArrays[param][i];
            }

            points.Add(new ForecastPoint(DateTimeOffset.FromUnixTimeSeconds(times[i]), dict));
        }

        var lastValued = points.FindLastIndex(p => p.HasAnyValue);
        return lastValued < 0 ? null : new ForecastSeries(points.Take(lastValued + 1));
    }

    private DailyForecastSeries MapDaily(OpenMeteoTimeSeries series)
    {
        var times = series.Time;
        var paramArrays = new Dictionary<ParameterDef, IReadOnlyList<object?>>(parameters.Daily.Count);

        foreach (var param in parameters.Daily)
        {
            paramArrays[param] = series.Variables.TryGetValue(param.ApiName, out var element)
                ? ParseMixedArray(element, times.Count)
                : NullObjectArray(times.Count);
        }

        var points = new List<DailyForecastPoint>(times.Count);
        for (var i = 0; i < times.Count; i++)
        {
            var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(times[i]).UtcDateTime);
            var numeric = new Dictionary<ParameterDef, double?>();
            var meta = new Dictionary<ParameterDef, string?>();

            foreach (var param in parameters.Daily)
            {
                var raw = i < paramArrays[param].Count ? paramArrays[param][i] : null;
                if (param.ValueType == ParameterValueType.TimeString)
                {
                    meta[param] = raw switch
                    {
                        string s => s,
                        double d => DateTimeOffset.FromUnixTimeSeconds((long)d).ToString("O"),
                        _ => null,
                    };
                }
                else
                {
                    numeric[param] = raw as double?;
                }
            }

            var point = new DailyForecastPoint(date, numeric, meta);
            if (point.HasAnyValue)
            {
                points.Add(point);
            }
        }

        return new DailyForecastSeries(points);
    }

    private string? UnitMismatch(Dictionary<string, string>? units)
    {
        if (units is null || units.Count == 0)
        {
            return "Response carried no hourly_units";
        }

        if (units.TryGetValue("time", out var timeUnit) && timeUnit != "unixtime")
        {
            return $"Unexpected unit '{timeUnit}' for time (expected 'unixtime')";
        }

        foreach (var param in parameters.Hourly)
        {
            var expected = param switch
            {
                { Unit: "°C", DeviceClass: "temperature" } => "°C",
                { Unit: "m/s", DeviceClass: "wind_speed" } => "m/s",
                { Unit: "hPa", DeviceClass: "atmospheric_pressure" } => "hPa",
                _ => null,
            };

            if (expected is null)
            {
                continue;
            }

            if (units.TryGetValue(param.ApiName, out var actual) && actual != expected && actual != "undefined")
            {
                return $"Unexpected unit '{actual}' for {param.ApiName} (expected '{expected}')";
            }
        }

        return null;
    }

    private static IReadOnlyList<double?> ParseNumberArray(JsonElement element, int expectedLength)
    {
        var result = new List<double?>(expectedLength);
        foreach (var item in element.EnumerateArray())
        {
            result.Add(item.ValueKind == JsonValueKind.Null ? null : item.GetDouble());
        }

        return result;
    }

    private static IReadOnlyList<object?> ParseMixedArray(JsonElement element, int expectedLength)
    {
        var result = new List<object?>(expectedLength);
        foreach (var item in element.EnumerateArray())
        {
            result.Add(item.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number => item.GetDouble(),
                JsonValueKind.String => item.GetString(),
                _ => null,
            });
        }
        return result;
    }

    private static double?[] NullArray(int count)
    {
        return new double?[count];
    }

    private static object?[] NullObjectArray(int count)
    {
        return new object?[count];
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
