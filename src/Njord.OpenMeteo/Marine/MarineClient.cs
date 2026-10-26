using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Marine;

public sealed class MarineClient : IDomainClient
{
    private readonly HttpClient _httpClient;
    private readonly MarineOptions _options;
    private readonly IReadOnlyList<string> _variables;

    public MarineClient(HttpClient httpClient, IOptions<MarineOptions> options, IOptions<FeatureRequirementsOptions> requirements)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _variables = requirements.Value.Requirements.AllMarine.ToApiNames();
    }

    public DomainKind Domain => DomainKind.Marine;

    public int CallWeight => 1;

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var uri = ClientRequest.Uri(OpenMeteoEndpoints.MarinePath, latitude, longitude,
            (ClientRequest.Hourly, ClientRequest.List(_variables)), ("forecast_days", ClientRequest.Number(_options.ForecastDays)));
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.MarineResponse, cancellationToken),
            response => response.ToDomainForecast(now)?.TrimTrailing((MarinePoint p, MarineParams f) => p.HasFields(f)),
            cancellationToken);
    }
}
