using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo;

public sealed class FloodClient : IDomainClient
{
    private readonly HttpClient _httpClient;
    private readonly FloodOptions _options;
    private readonly IReadOnlyList<string> _variables;

    public FloodClient(HttpClient httpClient, IOptions<FloodOptions> options, FetchPlan plan)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _variables = plan.Requirements.AllFlood.ToApiNames();
    }

    public DomainKind Domain => DomainKind.Flood;

    public int CallWeight => 1;

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = new List<(string, string)>
        {
            (ClientRequest.Daily, ClientRequest.List(_variables)),
            ("forecast_days", ClientRequest.Number(_options.ForecastDays)),
        };
        if (_options.IncludeEnsemble)
        {
            query.Add(("ensemble", "true"));
        }

        var uri = ClientRequest.Uri(OpenMeteoEndpoints.FloodPath, latitude, longitude, [.. query]);
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.FloodResponse, cancellationToken),
            response => response.ToDomainForecast(now),
            cancellationToken);
    }
}
