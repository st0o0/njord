using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Solar;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo;

public sealed class SatelliteRadiationClient : IDomainClient
{
    private readonly HttpClient _httpClient;
    private readonly SatelliteOptions _options;
    private readonly SolarYieldOptions _solar;
    private readonly IReadOnlyList<string> _variables;

    public SatelliteRadiationClient(HttpClient httpClient, IOptions<SatelliteOptions> options, IOptions<SolarYieldOptions> solar, ResolvedRequirements requirements)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _solar = solar.Value;
        _variables = requirements.Value.AllSatellite.ToApiNames();
    }

    public DomainKind Domain => DomainKind.Satellite;

    public int CallWeight => 1;

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Open-Meteo azimuth: 0 = south; SolarYieldOptions uses compass degrees (180 = south).
        var uri = ClientRequest.Uri("v1/archive", latitude, longitude,
            ("hourly", ClientRequest.List(_variables)),
            ("tilt", ClientRequest.Number(_solar.Tilt)),
            ("azimuth", ClientRequest.Number(_solar.Azimuth - 180)),
            ("past_hours", ClientRequest.Number(_options.PastHours)),
            ("forecast_hours", "1"));
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.SatelliteRadiationResponse, cancellationToken),
            response => response.ToDomainForecast(now)?.TrimTrailing((SatelliteRadiationPoint p, SatelliteParams f) => p.HasFields(f)),
            cancellationToken);
    }
}
