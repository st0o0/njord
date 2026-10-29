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
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Http;

namespace Njord.OpenMeteo.Ensemble;

public sealed class EnsembleClient : IDomainClient
{
    private const int MaxForecastDays = 35;

    private readonly HttpClient _httpClient;
    private readonly SolarYieldOptions _solar;
    private readonly IReadOnlyList<string> _weatherVariables;
    private readonly IReadOnlyList<string> _spreadVariables;
    private readonly IReadOnlyList<string> _allVariables;
    private readonly int _forecastDays;
    private readonly ImmutableArray<string> _modelIds;
    private readonly string _primaryModel;
    private readonly bool _needsModels;

    public EnsembleClient(HttpClient httpClient, IOptions<ForecastOptions> options, IOptions<SolarYieldOptions> solar, IOptions<FeatureRequirementsOptions> requirements, ILogger<EnsembleClient> logger)
    {
        _httpClient = httpClient;
        var resolved = requirements.Value;
        _modelIds = resolved.EnsembleModels;
        _primaryModel = _modelIds.IsDefaultOrEmpty ? ForecastOptions.DefaultModel : _modelIds[0];
        _needsModels = resolved.AllEnsemble.HasFlag(EnsembleParams.Models);

        var ignored = options.Value.Models.Except(_modelIds).ToArray();
        if (ignored.Length > 0)
        {
            logger.LogWarning("Ensemble models ignored because no enabled feature uses them: {Models}", string.Join(", ", ignored));
        }

        _solar = solar.Value;
        _spreadVariables = resolved.AllEnsemble.ToApiNames();
        _weatherVariables = _needsModels ? resolved.Requirements.AllForecast.ToApiNames() : [];
        _allVariables = [.. _weatherVariables.Concat(_spreadVariables).Distinct()];
        _forecastDays = Math.Min(options.Value.ForecastDays, MaxForecastDays);
        CallWeight = Core.Polling.CallWeight.Of(_allVariables.Count, _forecastDays) * _modelIds.Length;
    }

    public DomainKind Domain => DomainKind.Ensemble;

    public int CallWeight { get; }

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = new List<(string, string)>
        {
            (ClientRequest.Hourly, ClientRequest.List(_allVariables)),
            (ClientRequest.Models, ClientRequest.List(_modelIds)),
            (ClientRequest.WindSpeedUnit, ClientRequest.MetersPerSecond),
        };
        if (_needsModels && _weatherVariables.Contains(ForecastApiVariable.GlobalTiltedIrradiance.ToApiName()))
        {
            query.Add((ClientRequest.Tilt, ClientRequest.Number(_solar.Tilt)));
            query.Add((ClientRequest.Azimuth, ClientRequest.Number(ClientRequest.ToApiAzimuth(_solar.Azimuth))));
        }

        query.Add((ClientRequest.ForecastDays, ClientRequest.Number(_forecastDays)));
        var uri = ClientRequest.Uri(OpenMeteoEndpoints.EnsemblePath, latitude, longitude, [.. query]);
        return ClientRequest.ExecuteResultAsync(
            () => _httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.ForecastResponse, cancellationToken),
            response => ToSuccess(response, now),
            cancellationToken);
    }

    private FetchResult ToSuccess(ForecastResponse response, DateTimeOffset now)
    {
        var ensemble = response.ToEnsembleForecast(now, _primaryModel);
        if (ensemble is null)
        {
            return new FetchEmpty();
        }

        var result = new FetchSuccess(ensemble);

        if (_needsModels)
        {
            var modelForecasts = response.ToModelForecasts(now, _primaryModel);
            if (modelForecasts.Count > 0)
            {
                result = result with
                {
                    Models = modelForecasts.ToImmutableDictionary(kv => kv.Key, IDomainForecast (kv) => kv.Value),
                };
            }
        }

        return result;
    }
}
