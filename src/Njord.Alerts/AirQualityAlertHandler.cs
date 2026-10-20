using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts;

public sealed class AirQualityAlertHandler : IMultiFeatureHandler
{
    private readonly IReadOnlyList<IAlertRule<DomainForecast<AirQualityPoint>>> _rules;
    private readonly IOptions<AlertOptions> _options;
    private readonly Lazy<Enabled> _enabled;

    public AirQualityAlertHandler(
        IEnumerable<IAlertRule<DomainForecast<AirQualityPoint>>> rules,
        IOptions<AlertOptions> options,
        IOptions<FeatureOptions> features)
    {
        _rules = [.. rules.OrderBy(rule => rule.Name, StringComparer.Ordinal)];
        _options = options;
        Features = [.. _rules];
        _enabled = new Lazy<Enabled>(() => Resolve(features.Value));
    }

    public string Name => "AirQualityAlerts";

    public IReadOnlyList<IFeature> Features { get; }

    public FeatureDescriptor Feature => _enabled.Value.Composite;

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        var options = _options.Value;
        if (!options.Enabled || current.AirQuality is not { } airQuality)
        {
            return [];
        }

        var now = time.GetUtcNow();
        return
        [
            .. _enabled.Value.Rules
                .Select(rule => (Rule: rule, Alert: rule.Evaluate(airQuality, options, now)))
                .Where(pair => pair.Alert.Severity != AlertSeverity.None)
                .Select(pair => new AlertResult(current.Location, pair.Rule.WireKey(), pair.Alert, now)),
        ];
    }

    private Enabled Resolve(FeatureOptions features)
    {
        var rules = _rules.Where(rule => features.IsEnabled(rule)).ToList();
        var requirements = rules.Aggregate(default(DomainRequirements), (all, rule) => all | rule.Feature.Requirements);
        var representative = (rules.Count > 0 ? rules : _rules)[0].Feature;
        return new Enabled(rules, representative with { DefaultEnabled = rules.Count > 0, Requirements = requirements });
    }

    private sealed record Enabled(IReadOnlyList<IAlertRule<DomainForecast<AirQualityPoint>>> Rules, FeatureDescriptor Composite);
}
