using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts;

// One handler for all weather alerts: it runs only the rules whose feature is enabled. Its Feature is the composite
// of the enabled rules (their requirements united), resolved on first use because FeatureOptions are not available
// while the handler is constructed.
public sealed class WeatherAlertHandler : IMultiFeatureHandler
{
    private readonly IReadOnlyList<IAlertRule<DomainForecast<WeatherPoint>>> _rules;
    private readonly IOptions<AlertOptions> _options;
    private readonly Lazy<Enabled> _enabled;

    public WeatherAlertHandler(
        IEnumerable<IAlertRule<DomainForecast<WeatherPoint>>> rules,
        IOptions<AlertOptions> options,
        IOptions<FeatureOptions> features)
    {
        _rules = [.. rules.OrderBy(rule => rule.Type)];
        _options = options;
        Features = [.. _rules.Select(rule => rule.Feature)];
        _enabled = new Lazy<Enabled>(() => Resolve(features.Value));
    }

    public IReadOnlyList<FeatureDescriptor> Features { get; }

    public FeatureDescriptor Feature => _enabled.Value.Composite;

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        var options = _options.Value;
        if (!options.Enabled || current.Weather is not { } weather)
        {
            return [];
        }

        var now = time.GetUtcNow();
        return
        [
            .. _enabled.Value.Rules
                .Select(rule => rule.Evaluate(weather, options, now))
                .Where(alert => alert.Severity != AlertSeverity.None)
                .Select(alert => new PipelineEvent.AlertRaised(current.Location, alert, now)),
        ];
    }

    private Enabled Resolve(FeatureOptions features)
    {
        var rules = _rules.Where(rule => features.IsEnabled(rule.Feature)).ToList();
        var requirements = rules.Aggregate(default(DomainRequirements), (all, rule) => all | rule.Feature.Requirements);
        var representative = (rules.Count > 0 ? rules : _rules).First().Feature;
        return new Enabled(rules, representative with { DefaultEnabled = rules.Count > 0, Requirements = requirements });
    }

    private sealed record Enabled(IReadOnlyList<IAlertRule<DomainForecast<WeatherPoint>>> Rules, FeatureDescriptor Composite);
}
