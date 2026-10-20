using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo;

public sealed class AirQualityClient : IDomainClient
{
    private const int MaxForecastDays = 7;

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _variables;
    private readonly int _forecastDays;

    public AirQualityClient(HttpClient httpClient, IOptions<AirQualityOptions> options, ResolvedRequirements requirements)
    {
        _httpClient = httpClient;
        _variables = requirements.Value.AllAirQuality.ApplyRegionFilter(options.Value.Region).ToApiNames();
        _forecastDays = Math.Min(options.Value.ForecastDays, MaxForecastDays);
    }

    public DomainKind Domain => DomainKind.AirQuality;

    public int CallWeight => 1;

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var uri = ClientRequest.Uri("v1/air-quality", latitude, longitude,
            ("hourly", ClientRequest.List(_variables)), ("forecast_days", ClientRequest.Number(_forecastDays)));
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.AirQualityResponse, cancellationToken),
            response => response.ToDomainForecast(now)?.TrimTrailing((AirQualityPoint p, AirQualityParams f) => p.HasFields(f)),
            cancellationToken);
    }
}
