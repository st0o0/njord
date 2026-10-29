using System.Collections.Immutable;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Fetch;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Http;

namespace Njord.OpenMeteo.Forecast;

public sealed class ForecastClient : IDomainClient
{
    private const int MaxForecastDays = 16;
    private const string BestMatch = "best_match";

    private readonly HttpClient _httpClient;
    private readonly SatelliteOptions _solar;
    private readonly IReadOnlyList<string> _variables;
    private readonly int _forecastDays;
    private readonly int _pastHours;
    private readonly string[] _models;
    private readonly string _primaryModel;

    public ForecastClient(HttpClient httpClient, IOptions<ForecastOptions> options, IOptions<SatelliteOptions> solar, IOptions<DomainRequirements> requirements, ILogger<ForecastClient> logger)
    {
        _httpClient = httpClient;
        _solar = solar.Value;
        _variables = requirements.Value.AllForecast.ToApiNames();
        _forecastDays = Math.Min(options.Value.ForecastDays, MaxForecastDays);
        _pastHours = options.Value.PastHours;
        _models = options.Value.Models;
        _primaryModel = _models.Length > 0 ? _models[0] : BestMatch;
        CallWeight = (int)Math.Max(1, Math.Ceiling(Core.Polling.CallWeight.Of(_variables.Count, _forecastDays)));
    }

    public DomainKind Domain => DomainKind.Forecast;

    public int CallWeight { get; }

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = new List<(string, string)>
        {
            (ClientRequest.Hourly, ClientRequest.List(_variables)),
            (ClientRequest.WindSpeedUnit, ClientRequest.MetersPerSecond),
        };
        if (_variables.Contains(ForecastApiVariable.GlobalTiltedIrradiance.ToApiName()))
        {
            query.Add((ClientRequest.Tilt, ClientRequest.Number(_solar.Tilt)));
            query.Add((ClientRequest.Azimuth, ClientRequest.Number(ClientRequest.ToApiAzimuth(_solar.Azimuth))));
        }

        if (_models.Length > 0)
        {
            query.Add((ClientRequest.Models, ClientRequest.List(_models)));
        }

        if (_pastHours > 0)
        {
            query.Add((ClientRequest.PastHours, ClientRequest.Number(_pastHours)));
        }

        query.Add((ClientRequest.ForecastDays, ClientRequest.Number(_forecastDays)));
        var uri = ClientRequest.Uri(OpenMeteoEndpoints.ForecastPath, latitude, longitude, [.. query]);
        return ClientRequest.ExecuteResultAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.ForecastResponse, cancellationToken),
            response => ToSuccess(response.ToModelForecasts(now, _primaryModel)),
            cancellationToken);
    }

    private FetchResult ToSuccess(ImmutableDictionary<string, DomainForecast<ForecastPoint>> forecasts)
    {
        if (!forecasts.TryGetValue(_primaryModel, out var primary))
        {
            return new FetchEmpty();
        }

        var result = new FetchSuccess(primary);
        if (_models.Length > 1)
        {
            result = result with { Models = forecasts.ToImmutableDictionary(kv => kv.Key, IDomainForecast (kv) => kv.Value) };
        }

        return result;
    }
}
