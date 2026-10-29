using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Messages.Fetch;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Http;

namespace Njord.OpenMeteo.Seasonal;

public sealed class SeasonalClient : IDomainClient
{
    private readonly HttpClient _httpClient;
    private readonly SeasonalOptions _options;
    private readonly IReadOnlyList<string> _variables;

    public SeasonalClient(HttpClient httpClient, IOptions<SeasonalOptions> options, IOptions<DomainRequirements> requirements)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _variables = requirements.Value.AllSeasonal.ToApiNames();
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
