using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Njord.Configuration;
using Njord.Domain;

namespace Njord.Ingest;

public sealed class OpenMeteoClient(
    HttpClient httpClient,
    TimeProvider timeProvider,
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
                    HttpStatusCode.TooManyRequests => Failure(FetchFailureReason.RateLimited, "HTTP 429 from the forecast endpoint"),
                    HttpStatusCode.BadRequest => Failure(FetchFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => Failure(FetchFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the forecast endpoint"),
                };
            }

            var dto = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoForecastResponse, cancellationToken);
            if (dto?.Hourly is null or { ValueKind: JsonValueKind.Undefined })
                return Failure(FetchFailureReason.MalformedPayload, "Response contained no hourly data");

            if (UnitMismatch(dto.HourlyUnits) is { } mismatch)
                return Failure(FetchFailureReason.MalformedPayload, mismatch);

            var hourlyResult = MapHourly(dto.Hourly.Value);
            if (hourlyResult is null)
                return Failure(FetchFailureReason.MalformedPayload, "Response carried hourly timestamps but no values");

            var daily = dto.Daily is { ValueKind: not JsonValueKind.Undefined }
                ? MapDaily(dto.Daily.Value)
                : DailyForecastSeries.Empty;

            return new FetchOutcome.Success(new ModelForecast(
                model, location.Name, cycle, timeProvider.GetUtcNow(),
                hourlyResult, daily));
        }
        catch (HttpRequestException ex)
        {
            return Failure(FetchFailureReason.Transport, ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(FetchFailureReason.Transport, "Request timed out");
        }
        catch (JsonException)
        {
            return Failure(FetchFailureReason.MalformedPayload, "Response JSON did not match the expected schema");
        }

        FetchOutcome.Failure Failure(FetchFailureReason reason, string detail)
            => new(cycle, location.Name, model, reason, detail);
    }

    private string BuildUri(LocationOptions location, WeatherModel model)
    {
        var uri = string.Create(
            CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}" +
            $"&models={Uri.EscapeDataString(model.Id)}&hourly={_hourlyVariables}" +
            $"&wind_speed_unit=ms&timeformat=unixtime&forecast_days=4");

        if (_dailyVariables.Length > 0)
            uri += $"&daily={_dailyVariables}";

        return uri;
    }

    private ForecastSeries? MapHourly(JsonElement hourly)
    {
        if (!hourly.TryGetProperty("time", out var timeElement))
            return null;

        var times = new List<long>();
        foreach (var t in timeElement.EnumerateArray())
            times.Add(t.GetInt64());

        if (times.Count == 0)
            return null;

        var paramArrays = new Dictionary<ParameterDef, List<double?>>();
        foreach (var param in parameters.Hourly)
        {
            var values = new List<double?>(times.Count);
            if (hourly.TryGetProperty(param.ApiName, out var arr))
            {
                foreach (var v in arr.EnumerateArray())
                    values.Add(v.ValueKind == JsonValueKind.Null ? null : v.GetDouble());
            }
            else
            {
                for (var i = 0; i < times.Count; i++)
                    values.Add(null);
            }

            paramArrays[param] = values;
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
        if (lastValued < 0)
            return null;

        return new ForecastSeries(points.Take(lastValued + 1));
    }

    private DailyForecastSeries MapDaily(JsonElement daily)
    {
        if (!daily.TryGetProperty("time", out var timeElement))
            return DailyForecastSeries.Empty;

        var dates = new List<DateOnly>();
        foreach (var t in timeElement.EnumerateArray())
        {
            var dateStr = t.GetString();
            if (dateStr is not null && DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, out var date))
                dates.Add(date);
        }

        if (dates.Count == 0)
            return DailyForecastSeries.Empty;

        var paramArrays = new Dictionary<ParameterDef, List<object?>>();
        foreach (var param in parameters.Daily)
        {
            var values = new List<object?>(dates.Count);
            if (daily.TryGetProperty(param.ApiName, out var arr))
            {
                foreach (var v in arr.EnumerateArray())
                {
                    values.Add(v.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.Number => v.GetDouble(),
                        JsonValueKind.String => v.GetString(),
                        _ => null,
                    });
                }
            }
            else
            {
                for (var i = 0; i < dates.Count; i++)
                    values.Add(null);
            }

            paramArrays[param] = values;
        }

        var points = new List<DailyForecastPoint>(dates.Count);
        for (var i = 0; i < dates.Count; i++)
        {
            var dict = new Dictionary<ParameterDef, object?>(parameters.Daily.Count);
            foreach (var param in parameters.Daily)
            {
                if (i < paramArrays[param].Count)
                    dict[param] = paramArrays[param][i];
                else
                    dict[param] = null;
            }

            points.Add(new DailyForecastPoint(dates[i], dict));
        }

        return new DailyForecastSeries(points);
    }

    private string? UnitMismatch(JsonElement? units)
    {
        if (units is null or { ValueKind: JsonValueKind.Undefined })
            return "Response carried no hourly_units";

        var unitsObj = units.Value;

        if (TryGetString(unitsObj, "time") is { } timeUnit && timeUnit != "unixtime")
            return $"Unexpected unit '{timeUnit}' for time (expected 'unixtime')";

        foreach (var param in parameters.Hourly)
        {
            var expected = param.ApiName switch
            {
                _ when param.Unit == "°C" && param.DeviceClass == "temperature" => "°C",
                _ when param.Unit == "m/s" && param.DeviceClass == "wind_speed" => "m/s",
                _ when param.Unit == "hPa" && param.DeviceClass == "atmospheric_pressure" => "hPa",
                _ => null,
            };

            if (expected is null) continue;
            if (TryGetString(unitsObj, param.ApiName) is { } actual && actual != expected)
                return $"Unexpected unit '{actual}' for {param.ApiName} (expected '{expected}')";
        }

        return null;
    }

    private static string? TryGetString(JsonElement obj, string property)
        => obj.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

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
