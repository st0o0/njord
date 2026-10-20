using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.AirQuality;

public sealed class AirQualityAlertHandler(
    IEnumerable<IAlertRule<DomainForecast<AirQualityPoint>>> rules,
    IOptions<AlertOptions> options,
    AlertType alertType) : IDomainHandler
{
    private readonly IReadOnlyList<IAlertRule<DomainForecast<AirQualityPoint>>> _rules = [.. rules.Where(rule => rule.Type == alertType)];

    public string ConfigKey { get; } = alertType switch
    {
        AlertType.Pollen => "PollenAlert",
        AlertType.Pm25 => "Pm25Alert",
        _ => throw new ArgumentOutOfRangeException(nameof(alertType), alertType, "Not an air quality alert type."),
    };

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.AirQuality };

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
