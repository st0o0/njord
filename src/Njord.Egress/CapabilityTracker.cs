using Njord.Core.Configuration;
using Njord.Messages.Egress;
using Njord.Weather;

namespace Njord.Egress;

public sealed class CapabilityTracker
{
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly Dictionary<(string Location, string ModelId), HashSet<ParameterDef>> _known = [];

    public CapabilityTracker(ResolvedParameterSet parameters, IReadOnlyList<int> horizons, int forecastDays)
    {
        _parameters = parameters;
        _horizons = horizons;
        _forecastDays = forecastDays;
    }

    public EgressEvent.CapabilityLearned? Track(ModelForecast forecast)
    {
        var key = (forecast.Location, forecast.Model.Id);
        var observed = ExtractSupportedParameters(forecast, _parameters);

        if (!_known.TryGetValue(key, out var known))
        {
            known = new HashSet<ParameterDef>(observed);
            _known[key] = known;
        }
        else if (observed.IsSubsetOf(known))
        {
            return null;
        }
        else
        {
            known.UnionWith(observed);
        }

        var maxHours = ModelCoverageRegistry.Get(forecast.Model.Id)?.MaxForecastHours;
        return BuildCapabilityLearned(forecast, known, _horizons, _forecastDays, maxHours);
    }

    private static HashSet<ParameterDef> ExtractSupportedParameters(
        ModelForecast forecast, ResolvedParameterSet parameters)
    {
        var supported = new HashSet<ParameterDef>();

        foreach (var point in forecast.Hourly.Points)
        {
            foreach (var param in parameters.Hourly)
            {
                if (point.Get(param) is not null)
                {
                    supported.Add(param);
                }
            }
        }

        foreach (var point in forecast.Daily.Points)
        {
            foreach (var param in parameters.Daily)
            {
                if (param.ValueType == ParameterValueType.TimeString)
                {
                    if (point.GetMeta(param) is not null)
                    {
                        supported.Add(param);
                    }
                }
                else
                {
                    if (point.GetNumeric(param) is not null)
                    {
                        supported.Add(param);
                    }
                }
            }
        }

        return supported;
    }

    private static EgressEvent.CapabilityLearned BuildCapabilityLearned(
        ModelForecast forecast,
        HashSet<ParameterDef> supported,
        IReadOnlyList<int> horizons,
        int forecastDays,
        int? maxForecastHours)
    {
        var applicableHorizons = maxForecastHours.HasValue
            ? horizons.Where(h => h <= maxForecastHours.Value).ToList()
            : horizons.ToList();

        var maxDays = maxForecastHours.HasValue
            ? (int)Math.Ceiling(maxForecastHours.Value / 24.0)
            : forecastDays;
        var applicableDayOffsets = Enumerable.Range(0, Math.Min(forecastDays, maxDays)).ToList();

        return new EgressEvent.CapabilityLearned(
            forecast.Location,
            forecast.Model,
            supported.ToHashSet(),
            applicableHorizons,
            applicableDayOffsets);
    }
}
