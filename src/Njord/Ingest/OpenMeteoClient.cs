using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Njord.Configuration;
using Njord.Domain;

namespace Njord.Ingest;

public sealed class OpenMeteoClient(HttpClient httpClient, TimeProvider timeProvider) : IOpenMeteoClient
{
    // Exactly 9 hourly variables and 4 forecast days: at or below the free
    // tier's weighting thresholds (10 variables / 2 weeks), so every fetch
    // counts as 1.0 API calls.
    private const string HourlyVariables =
        "temperature_2m,apparent_temperature,precipitation,wind_speed_10m,wind_gusts_10m," +
        "dew_point_2m,relative_humidity_2m,cloud_cover,pressure_msl";

    public async Task<FetchOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
    {
        var uri = string.Create(
            CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}" +
            $"&models={Uri.EscapeDataString(model.Id)}&hourly={HourlyVariables}" +
            $"&wind_speed_unit=ms&timeformat=unixtime&forecast_days=4");

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => Failure(FetchFailureReason.RateLimited, "HTTP 429 from the forecast endpoint"),
                    // 400 covers both unknown model ids and models outside their
                    // geographic coverage (verified) — either way this (location,
                    // model) pair yields nothing this cycle.
                    HttpStatusCode.BadRequest => Failure(FetchFailureReason.ModelUnavailable, await ReadErrorReasonAsync(response, cancellationToken)),
                    _ => Failure(FetchFailureReason.Transport, $"HTTP {(int)response.StatusCode} from the forecast endpoint"),
                };
            }

            var dto = await response.Content.ReadFromJsonAsync(OpenMeteoJsonContext.Default.OpenMeteoForecastResponse, cancellationToken);
            if (dto?.Hourly is null || dto.Hourly.Time.Count == 0)
                return Failure(FetchFailureReason.MalformedPayload, "Response contained no hourly data");

            if (UnitMismatch(dto.HourlyUnits) is { } mismatch)
                return Failure(FetchFailureReason.MalformedPayload, mismatch);

            return Map(dto.Hourly);
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

        FetchOutcome Map(OpenMeteoHourly hourly)
        {
            var points = new List<ForecastPoint>(hourly.Time.Count);
            for (var i = 0; i < hourly.Time.Count; i++)
            {
                points.Add(new ForecastPoint(
                    DateTimeOffset.FromUnixTimeSeconds(hourly.Time[i]),
                    Temperature: Value(hourly.Temperature2m, i),
                    ApparentTemperature: Value(hourly.ApparentTemperature, i),
                    Precipitation: Value(hourly.Precipitation, i),
                    WindSpeed: Value(hourly.WindSpeed10m, i),
                    WindGust: Value(hourly.WindGusts10m, i),
                    Dewpoint: Value(hourly.DewPoint2m, i),
                    RelativeHumidity: Value(hourly.RelativeHumidity2m, i),
                    CloudCover: Value(hourly.CloudCover, i),
                    PressureMsl: Value(hourly.PressureMsl, i)));
            }

            // Values beyond the model's forecast horizon arrive as an all-null
            // tail; points inside the horizon survive with individual gaps.
            var lastValued = points.FindLastIndex(HasAnyValue);
            if (lastValued < 0)
                return Failure(FetchFailureReason.MalformedPayload, "Response carried hourly timestamps but no values");

            return new FetchOutcome.Success(new ModelForecast(
                model, location.Name, cycle, timeProvider.GetUtcNow(),
                new ForecastSeries(points.Take(lastValued + 1))));
        }
    }

    private static bool HasAnyValue(ForecastPoint point)
        => Enum.GetValues<WeatherParameter>().Any(parameter => point.Get(parameter) is not null);

    private static string? UnitMismatch(OpenMeteoHourlyUnits? units)
    {
        if (units is null)
            return "Response carried no hourly_units";

        (string Field, string? Actual, string Expected)[] checks =
        [
            ("time", units.Time, "unixtime"),
            ("temperature_2m", units.Temperature2m, "°C"),
            ("apparent_temperature", units.ApparentTemperature, "°C"),
            ("wind_speed_10m", units.WindSpeed10m, "m/s"),
            ("wind_gusts_10m", units.WindGusts10m, "m/s"),
        ];
        var mismatch = checks.FirstOrDefault(c => c.Actual is not null && c.Actual != c.Expected);
        return mismatch.Field is null
            ? null
            : $"Unexpected unit '{mismatch.Actual}' for {mismatch.Field} (expected '{mismatch.Expected}')";
    }

    private static double? Value(IReadOnlyList<double?>? series, int index)
        => series is not null && index < series.Count ? series[index] : null;

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
