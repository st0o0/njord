using System.Collections.Immutable;
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

public sealed class WeatherClient : IDomainClient
{
    private const string GlobalTiltedIrradiance = "global_tilted_irradiance";
    private const int MaxForecastDays = 35;

    private readonly HttpClient _httpClient;
    private readonly SolarYieldOptions _solar;
    private readonly IReadOnlyList<string> _variables;
    private readonly int _forecastDays;
    private readonly ImmutableArray<string> _modelIds;
    private readonly string _primaryModel;

    public WeatherClient(HttpClient httpClient, IOptions<WeatherOptions> options, IOptions<SolarYieldOptions> solar, IOptions<FeatureRequirementsOptions> requirements, ILogger<WeatherClient> logger)
    {
        _httpClient = httpClient;
        var resolved = requirements.Value;
        _modelIds = resolved.WeatherModels;
        _primaryModel = resolved.PrimaryWeatherModel;
        var ignored = options.Value.EffectiveModels.Except(_modelIds).ToArray();
        if (ignored.Length > 0)
        {
            logger.LogWarning("Weather models ignored because no enabled feature uses them: {Models}", string.Join(", ", ignored));
        }

        _solar = solar.Value;
        _variables = resolved.Requirements.AllWeather.ToApiNames();
        _forecastDays = Math.Min(options.Value.ForecastDays, MaxForecastDays);
        CallWeight = Core.Polling.CallWeight.Of(_variables.Count, _forecastDays) * _modelIds.Length;
    }

    public DomainKind Domain => DomainKind.Weather;

    public int CallWeight { get; }

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = new List<(string, string)>
        {
            (ClientRequest.Hourly, ClientRequest.List(_variables)),
            (ClientRequest.Models, ClientRequest.List(_modelIds)),
            ("wind_speed_unit", "ms"),
        };
        if (_variables.Contains(GlobalTiltedIrradiance))
        {
            // Open-Meteo azimuth: 0 = south; SolarYieldOptions uses compass degrees (180 = south).
            query.Add(("tilt", ClientRequest.Number(_solar.Tilt)));
            query.Add(("azimuth", ClientRequest.Number(_solar.Azimuth - 180)));
        }

        query.Add(("forecast_days", ClientRequest.Number(_forecastDays)));
        var uri = ClientRequest.Uri(OpenMeteoEndpoints.WeatherPath, latitude, longitude, [.. query]);
        return ClientRequest.ExecuteResultAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.WeatherResponse, cancellationToken),
            response => ToSuccess(response.ToModelForecasts(now, _primaryModel)),
            cancellationToken);
    }

    private FetchResult ToSuccess(ImmutableDictionary<string, DomainForecast<WeatherPoint>> forecasts)
    {
        // The primary model leads; when it returned nothing the first remaining model does not stand in for it.
        if (!forecasts.TryGetValue(_primaryModel, out var primary))
        {
            return new FetchEmpty();
        }

        return new FetchSuccess(primary)
        {
            Models = forecasts.ToImmutableDictionary(kv => kv.Key, IDomainForecast (kv) => kv.Value),
        };
    }
}
