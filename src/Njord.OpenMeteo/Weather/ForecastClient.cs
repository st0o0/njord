using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Weather;

public sealed class ForecastClient : IDomainClient
{
    private const int MaxForecastDays = 16;
    private const string BestMatch = "best_match";

    private readonly HttpClient _httpClient;
    private readonly SolarYieldOptions _solar;
    private readonly IReadOnlyList<string> _variables;
    private readonly int _forecastDays;

    public ForecastClient(HttpClient httpClient, IOptions<WeatherOptions> options, IOptions<SolarYieldOptions> solar, IOptions<FeatureRequirementsOptions> requirements, ILogger<ForecastClient> logger)
    {
        _httpClient = httpClient;
        _solar = solar.Value;
        _variables = requirements.Value.Requirements.AllWeather.ToApiNames();
        _forecastDays = Math.Min(options.Value.ForecastDays, MaxForecastDays);
        CallWeight = Core.Polling.CallWeight.Of(_variables.Count, _forecastDays);
    }

    public DomainKind Domain => DomainKind.Weather;

    public int CallWeight { get; }

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = new List<(string, string)>
        {
            (ClientRequest.Hourly, ClientRequest.List(_variables)),
            (ClientRequest.WindSpeedUnit, ClientRequest.MetersPerSecond),
        };
        if (_variables.Contains(WeatherApiVariable.GlobalTiltedIrradiance.ToApiName()))
        {
            query.Add((ClientRequest.Tilt, ClientRequest.Number(_solar.Tilt)));
            query.Add((ClientRequest.Azimuth, ClientRequest.Number(ClientRequest.ToApiAzimuth(_solar.Azimuth))));
        }

        query.Add((ClientRequest.ForecastDays, ClientRequest.Number(_forecastDays)));
        var uri = ClientRequest.Uri(OpenMeteoEndpoints.ForecastPath, latitude, longitude, [.. query]);
        return ClientRequest.ExecuteResultAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.WeatherResponse, cancellationToken),
            response => ToSuccess(response.ToModelForecasts(now, BestMatch)),
            cancellationToken);
    }

    private static FetchResult ToSuccess(System.Collections.Immutable.ImmutableDictionary<string, DomainForecast<WeatherPoint>> forecasts)
    {
        if (!forecasts.TryGetValue(BestMatch, out var primary))
        {
            return new FetchEmpty();
        }

        return new FetchSuccess(primary);
    }
}
