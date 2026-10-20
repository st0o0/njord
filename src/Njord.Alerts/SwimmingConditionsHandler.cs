using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts;

public sealed class SwimmingConditionsHandler(
    IAlertRule<DomainForecast<MarinePoint>> rule,
    IOptions<AlertOptions> options) : IDomainHandler
{
    public string Name => rule.Name;

    public FeatureDescriptor Feature => rule.Feature;

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (!options.Value.Enabled || current.Marine is not { } marine)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var alert = rule.Evaluate(marine, options.Value, now);
        return alert.Severity == AlertSeverity.None ? [] : [new AlertResult(current.Location, rule.WireKey(), alert, now)];
    }
}
