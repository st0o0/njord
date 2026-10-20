using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Flood;

public sealed class FloodRainHandler(IAlertRule<FloodRainInput> rule, IOptions<AlertOptions> options) : IDomainHandler
{
    public FeatureDescriptor Feature => rule.Feature;

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (!options.Value.Enabled || current is not { Flood: { } flood, Weather: { } weather })
        {
            return [];
        }

        var now = time.GetUtcNow();
        var alert = rule.Evaluate(new FloodRainInput(flood, weather), options.Value, now);
        return alert.Severity == AlertSeverity.None ? [] : [new PipelineEvent.AlertRaised(current.Location, alert, now)];
    }
}
