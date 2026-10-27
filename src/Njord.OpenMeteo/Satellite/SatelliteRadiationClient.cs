using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Solar;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Satellite;

public sealed class SatelliteRadiationClient : IDomainClient
{
    private readonly HttpClient _httpClient;
    private readonly SatelliteOptions _options;
    private readonly SolarYieldOptions _solar;
    private readonly IReadOnlyList<string> _variables;

    public SatelliteRadiationClient(HttpClient httpClient, IOptions<SatelliteOptions> options, IOptions<SolarYieldOptions> solar, IOptions<FeatureRequirementsOptions> requirements)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _solar = solar.Value;
        _variables = requirements.Value.Requirements.AllSatellite.ToApiNames();
    }

    public DomainKind Domain => DomainKind.Satellite;

    public int CallWeight => 1;

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var uri = ClientRequest.Uri(OpenMeteoEndpoints.SatellitePath, latitude, longitude,
            (ClientRequest.Hourly, ClientRequest.List(_variables)),
            (ClientRequest.Tilt, ClientRequest.Number(_solar.Tilt)),
            (ClientRequest.Azimuth, ClientRequest.Number(ClientRequest.ToApiAzimuth(_solar.Azimuth))),
            (ClientRequest.PastHours, ClientRequest.Number(_options.PastHours)),
            (ClientRequest.ForecastHours, "1"));
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.SatelliteRadiationResponse, cancellationToken),
            response => response.ToDomainForecast(now)?.TrimTrailing((SatelliteRadiationPoint p, SatelliteParams f) => p.HasFields(f)),
            cancellationToken);
    }
}
