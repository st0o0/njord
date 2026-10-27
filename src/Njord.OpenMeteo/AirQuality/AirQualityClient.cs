using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.AirQuality;

public sealed class AirQualityClient : IDomainClient
{
    private const int MaxForecastDays = 7;

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _variables;
    private readonly int _forecastDays;

    public AirQualityClient(HttpClient httpClient, IOptions<AirQualityOptions> options, IOptions<FeatureRequirementsOptions> requirements)
    {
        _httpClient = httpClient;
        _variables = requirements.Value.Requirements.AllAirQuality.ApplyRegionFilter(options.Value.Region).ToApiNames();
        _forecastDays = Math.Min(options.Value.ForecastDays, MaxForecastDays);
    }

    public DomainKind Domain => DomainKind.AirQuality;

    public int CallWeight => 1;

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var uri = ClientRequest.Uri(OpenMeteoEndpoints.AirQualityPath, latitude, longitude,
            (ClientRequest.Hourly, ClientRequest.List(_variables)), (ClientRequest.ForecastDays, ClientRequest.Number(_forecastDays)));
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.AirQualityResponse, cancellationToken),
            response => response.ToDomainForecast(now)?.TrimTrailing((AirQualityPoint p, AirQualityParams f) => p.HasFields(f)),
            cancellationToken);
    }
}
