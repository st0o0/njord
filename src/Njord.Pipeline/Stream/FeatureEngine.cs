using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Pipeline.Stream;

internal sealed class FeatureEngine
{
    private readonly IReadOnlyList<IFeatureRule> _enabled;
    private readonly int _ringDepth;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, RingBuffer<LocationForecast>> _rings = new(StringComparer.OrdinalIgnoreCase);

    public FeatureEngine(IEnumerable<IFeatureRule> allFeatures, TimeProvider time)
    {
        _enabled = [.. allFeatures.Where(f => f.Enabled)];
        _ringDepth = _enabled.Count == 0 ? 1 : _enabled.Max(f => f.Descriptor.PollDepth) + 1;
        _time = time;
    }

    public IReadOnlyList<PipelineEvent> Process(DomainDataReceived received)
    {
        var events = new List<PipelineEvent>(4) { received.ToPipelineEvent() };

        if (!_rings.TryGetValue(received.Location.Name, out var ring))
        {
            ring = new RingBuffer<LocationForecast>(_ringDepth);
            _rings[received.Location.Name] = ring;
        }

        if (received.ModelId is { } modelId)
        {
            if (received.Data is DomainForecast<ForecastPoint> modelWeather)
            {
                var latest = ring.Count > 0 ? ring[0] : new LocationForecast(received.Location);
                latest = received.Domain == DomainKind.Forecast
                    ? latest.WithForecastModel(modelId, modelWeather)
                    : latest.WithEnsembleModel(modelId, modelWeather);
                if (ring.Count > 0)
                {
                    ring.ReplaceNewest(latest);
                }
                else
                {
                    ring.Push(latest);
                }
            }

            return events;
        }

        var current =
            (ring.Count > 0 ? ring[0] : new LocationForecast(received.Location)).With(received.Domain, received.Data);
        current = current with
        {
            ForecastModels = ring.Count > 0 ? ring[0].ForecastModels : null,
            EnsembleModels = ring.Count > 0 ? ring[0].EnsembleModels : null,
        };
        ring.Push(current);

        var now = _time.GetUtcNow();
        foreach (var feature in _enabled)
        {
            var requirements = feature.Descriptor.Requirements;
            var passive = !requirements.RequiresAnyDomain();
            if (!passive && !requirements.Requires(received.Domain))
            {
                continue;
            }

            if (!passive && !current.HasFields(requirements))
            {
                Telemetry.HandlerSkipped.Add(1,
                    new KeyValuePair<string, object?>("handler", feature.GetType().Name),
                    new KeyValuePair<string, object?>("reason", "missing_data"));
                continue;
            }

            var results = feature.ExecuteAll(ring, now);
            if (results.Count > 0)
            {
                events.AddRange(results);
            }
        }

        return events;
    }
}
