using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Seasonal;

public sealed class SeasonalClient : IDomainClient
{
    private readonly HttpClient _httpClient;
    private readonly SeasonalOptions _options;
    private readonly IReadOnlyList<string> _variables;

    public SeasonalClient(HttpClient httpClient, IOptions<SeasonalOptions> options, FetchPlan plan)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _variables = plan.Requirements.AllSeasonal.ToApiNames();
    }

    public DomainKind Domain => DomainKind.Seasonal;

    public int CallWeight => 1;

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var uri = ClientRequest.Uri(OpenMeteoEndpoints.SeasonalPath, latitude, longitude, (ClientRequest.Monthly, ClientRequest.List(_variables)));
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.SeasonalResponse, cancellationToken),
            response => response.ToDomainForecast(now, _options.ForecastMonths),
            cancellationToken);
    }
}
