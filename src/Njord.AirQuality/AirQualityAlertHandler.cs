using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.AirQuality;

public sealed class AirQualityAlertHandler(
    IEnumerable<IAlertRule<DomainForecast<AirQualityPoint>>> rules,
    IOptions<AlertOptions> options,
    AlertType alertType) : IDomainHandler
{
    private readonly IReadOnlyList<IAlertRule<DomainForecast<AirQualityPoint>>> _rules = [.. rules.Where(rule => rule.Type == alertType)];

    public FeatureDescriptor Feature { get; } = rules.Single(rule => rule.Type == alertType).Feature;

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
                .Select(rule => rule.Evaluate(airQuality, options.Value, now))
                .Where(alert => alert.Severity != AlertSeverity.None)
                .Select(alert => new PipelineEvent.AlertRaised(current.Location, alert, now)),
        ];
    }
}
