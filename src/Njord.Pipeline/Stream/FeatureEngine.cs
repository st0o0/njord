using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline;

// The analysis state of the pipeline: the latest LocationForecast per location and a ring of the most recent ones,
// deep enough for the feature with the longest PollDepth. Lives inside the StatefulSelectMany closure.
internal sealed class FeatureEngine
{
    private readonly IReadOnlyList<IFeatureRule> _enabled;
    private readonly int _ringDepth;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, RingBuffer<LocationForecast>> _rings = new(StringComparer.OrdinalIgnoreCase);

    public FeatureEngine(IEnumerable<IFeatureRule> allFeatures, TimeProvider time)
    {
        _enabled = FeatureResolution.EnabledFeatures(allFeatures);
        _ringDepth = _enabled.Count == 0 ? 1 : _enabled.Max(f => f.Descriptor.PollDepth) + 1;
        _time = time;
    }

    public DomainRequirements Requirements => FeatureResolution.RequiredParams(_enabled);

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
            if (received.Data is DomainForecast<Njord.Forecast.Weather.WeatherPoint> modelWeather)
            {
                var latest = (ring.Count > 0 ? ring[0] : new LocationForecast(received.Location)).WithModel(modelId, modelWeather);
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

        var current = (ring.Count > 0 ? ring[0] : new LocationForecast(received.Location)).With(received.Domain, received.Data);
        current = current with { ModelWeather = ring.Count > 0 ? ring[0].ModelWeather : null };
        ring.Push(current);

        var now = _time.GetUtcNow();
        foreach (var feature in _enabled)
        {
            var requirements = feature.Descriptor.Requirements;
            if (!requirements.Requires(received.Domain))
            {
                continue;
            }

            if (!current.HasFields(requirements))
            {
                Telemetry.HandlerSkipped.Add(1,
                    new KeyValuePair<string, object?>("handler", feature.GetType().Name),
                    new KeyValuePair<string, object?>("reason", "missing_data"));
                continue;
            }

            events.AddRange(feature.Execute(ring, now));
        }

        return events;
    }
}
