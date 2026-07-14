using System.Globalization;
using System.Net;
using System.Text.Json;
using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Ingest;

public sealed class OpenMeteoClient(
    HttpClient httpClient,
    ResolvedParameterSet parameters) : IOpenMeteoClient
{
    private readonly string _hourlyVariables = string.Join(",", parameters.Hourly.Select(p => p.ApiName));
    private readonly string _dailyVariables = string.Join(",", parameters.Daily.Select(p => p.ApiName));

    public async Task<FetchOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
    {
        var uri = BuildUri(location, model);

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => new FetchOutcome.Failure(location.Name, model, FetchFailureReason.RateLimited, "HTTP 429 from the forecast endpoint"),
                    HttpStatusCode.BadRequest => new FetchOutcome.Failure(location.Name, model, FetchFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => new FetchOutcome.Failure(location.Name, model, FetchFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the forecast endpoint"),
                };
            }

            var dto = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoForecastResponse, cancellationToken);
            if (dto?.Hourly is null || dto.Hourly.Time.Count == 0)
            {
                return new FetchOutcome.Failure(location.Name, model, FetchFailureReason.MalformedPayload, "Response contained no hourly data");
            }

            if (UnitMismatch(dto.HourlyUnits) is { } mismatch)
            {
                return new FetchOutcome.Failure(location.Name, model, FetchFailureReason.MalformedPayload, mismatch);
            }

            var hourlyResult = MapHourly(dto.Hourly);
            if (hourlyResult is null)
            {
                return new FetchOutcome.Failure(location.Name, model, FetchFailureReason.MalformedPayload, "Response carried hourly timestamps but no values");
            }

            var daily = dto.Daily is { Time.Count: > 0 }
                ? MapDaily(dto.Daily)
                : DailyForecastSeries.Empty;

            return new FetchOutcome.Success(new ModelForecast(
                model, location.Name, cycle,
                hourlyResult, daily));
        }
        catch (HttpRequestException ex)
        {
            return new FetchOutcome.Failure(location.Name, model, FetchFailureReason.Transport, ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new FetchOutcome.Failure(location.Name, model, FetchFailureReason.Transport, "Request timed out");
        }
        catch (JsonException)
        {
            return new FetchOutcome.Failure(location.Name, model, FetchFailureReason.MalformedPayload, "Response JSON did not match the expected schema");
        }
    }

    private string BuildUri(LocationOptions location, WeatherModel model)
    {
        var uri = string.Create(
            CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}" +
            $"&models={Uri.EscapeDataString(model.Id)}&hourly={_hourlyVariables}" +
            $"&wind_speed_unit=ms&timeformat=unixtime&forecast_days=4");

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
                dict[param] = paramArrays[param][i];

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
            var dict = new Dictionary<ParameterDef, object?>(parameters.Daily.Count);
            foreach (var param in parameters.Daily)
                dict[param] = i < paramArrays[param].Count ? paramArrays[param][i] : null;

            points.Add(new DailyForecastPoint(date, dict));
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

            if (units.TryGetValue(param.ApiName, out var actual) && actual != expected)
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
            result.Add(item.ValueKind == JsonValueKind.Null ? null : item.GetDouble());
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

    private static IReadOnlyList<double?> NullArray(int count)
    {
        var result = new double?[count];
        return result;
    }

    private static IReadOnlyList<object?> NullObjectArray(int count)
    {
        var result = new object?[count];
        return result;
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
