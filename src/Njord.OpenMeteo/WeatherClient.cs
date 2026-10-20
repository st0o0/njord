using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo;

public sealed class WeatherClient : IDomainClient
{
    private const string GlobalTiltedIrradiance = "global_tilted_irradiance";
    private const int MaxForecastDays = 35;

    private readonly HttpClient _httpClient;
    private readonly WeatherOptions _options;
    private readonly SolarYieldOptions _solar;
    private readonly IReadOnlyList<string> _variables;
    private readonly int _forecastDays;

    public WeatherClient(HttpClient httpClient, IOptions<WeatherOptions> options, IOptions<SolarYieldOptions> solar, ResolvedRequirements requirements)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _solar = solar.Value;
        _variables = requirements.Value.AllWeather.ToApiNames();
        _forecastDays = Math.Min(_options.ForecastDays, MaxForecastDays);
        CallWeight = Core.Polling.CallWeight.Of(_variables.Count, _forecastDays);
    }

    public DomainKind Domain => DomainKind.Weather;

    public int CallWeight { get; }

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = new List<(string, string)>
        {
            ("hourly", ClientRequest.List(_variables)),
            ("models", _options.Model),
            ("wind_speed_unit", "ms"),
        };
        if (_variables.Contains(GlobalTiltedIrradiance))
        {
            // Open-Meteo azimuth: 0 = south; SolarYieldOptions uses compass degrees (180 = south).
            query.Add(("tilt", ClientRequest.Number(_solar.Tilt)));
            query.Add(("azimuth", ClientRequest.Number(_solar.Azimuth - 180)));
        }

        query.Add(("forecast_days", ClientRequest.Number(_forecastDays)));
        var uri = ClientRequest.Uri("v1/ensemble", latitude, longitude, [.. query]);
        return ClientRequest.ExecuteAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.WeatherResponse, cancellationToken),
            response => response.ToDomainForecast(now)?.TrimTrailing((WeatherPoint p, WeatherParams f) => p.HasFields(f)),
            cancellationToken);
    }
}
