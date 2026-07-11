using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Njord.Configuration;
using Njord.Domain;

namespace Njord.Ingest;

public sealed class KachelmannClient(HttpClient httpClient, TimeProvider timeProvider) : IKachelmannClient
{
    public async Task<FetchOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
    {
        var uri = string.Create(
            CultureInfo.InvariantCulture,
            $"forecast/{location.Latitude}/{location.Longitude}/advanced/3h?model={Uri.EscapeDataString(model.Id)}&units=metric");

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Details stay generic on purpose: response bodies of failed calls
                // are never echoed into outcomes, so the API key cannot leak.
                return Failure(MapStatus(response.StatusCode), $"HTTP {(int)response.StatusCode} from the advanced forecast endpoint");
            }

            var dto = await response.Content.ReadFromJsonAsync(KachelmannJsonContext.Default.AdvancedForecastResponse, cancellationToken);
            if (dto is null || dto.Data.Count == 0)
                return Failure(FetchFailureReason.MalformedPayload, "Response contained no forecast data");

            return new FetchOutcome.Success(Map(dto));
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

        ModelForecast Map(AdvancedForecastResponse dto) => new(
            model,
            location.Name,
            cycle,
            timeProvider.GetUtcNow(),
            new ForecastSeries(dto.Data.Select(p => new ForecastPoint(
                p.DateTime,
                Temperature: p.Temp,
                Precipitation: p.PrecCurrent,
                WindSpeed: p.WindSpeed,
                WindGust: p.WindGust,
                Dewpoint: p.Dewpoint,
                RelativeHumidity: p.HumidityRelative,
                CloudCover: p.CloudCoverage,
                PressureMsl: p.PressureMsl))));
    }

    private static FetchFailureReason MapStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => FetchFailureReason.AuthFailed,
        HttpStatusCode.TooManyRequests => FetchFailureReason.RateLimited,
        // The only per-request variable is the model id (locations are validated
        // config), so client errors are attributed to the model.
        HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity
            => FetchFailureReason.ModelUnavailable,
        _ => FetchFailureReason.Transport,
    };
}
