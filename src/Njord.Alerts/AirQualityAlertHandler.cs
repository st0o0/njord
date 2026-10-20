using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts;

public sealed class AirQualityAlertHandler(
    IEnumerable<IAlertRule<DomainForecast<AirQualityPoint>>> rules,
    IOptions<AlertOptions> options,
    string name) : IDomainHandler
{
    private readonly IReadOnlyList<IAlertRule<DomainForecast<AirQualityPoint>>> _rules = [.. rules.Where(rule => rule.Name == name)];

    public string Name => name;

    public FeatureDescriptor Feature { get; } = rules.Single(rule => rule.Name == name).Feature;

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (!options.Value.Enabled || current.AirQuality is not { } airQuality)
        {
            return [];
        }

        var now = time.GetUtcNow();
        return
        [
            .. _rules
                .Select(rule => (Rule: rule, Alert: rule.Evaluate(airQuality, options.Value, now)))
                .Where(pair => pair.Alert.Severity != AlertSeverity.None)
                .Select(pair => new AlertResult(current.Location, pair.Rule.WireKey(), pair.Alert, now)),
        ];
    }
}
