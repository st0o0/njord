using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Fetch;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Http;

namespace Njord.OpenMeteo.Flood;

public sealed class FloodClient : IDomainClient
{
    private readonly HttpClient _httpClient;
    private readonly FloodOptions _options;
    private readonly IReadOnlyList<string> _variables;

    public FloodClient(HttpClient httpClient, IOptions<FloodOptions> options, IOptions<DomainRequirements> requirements)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _variables = requirements.Value.AllFlood.ToApiNames();
    }

    public DomainKind Domain => DomainKind.Flood;

    public int CallWeight => 1;

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = new List<(string, string)>
        {
            (ClientRequest.Daily, ClientRequest.List(_variables)),
            (ClientRequest.ForecastDays, ClientRequest.Number(_options.ForecastDays)),
        };
        if (_options.IncludeEnsemble)
        {
            query.Add((ClientRequest.Ensemble, "true"));
        }

        var uri = ClientRequest.Uri(OpenMeteoEndpoints.FloodPath, latitude, longitude, [.. query]);
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.FloodResponse, cancellationToken),
            response => response.ToDomainForecast(now),
            cancellationToken);
    }
}
