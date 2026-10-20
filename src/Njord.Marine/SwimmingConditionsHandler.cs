using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Marine;

public sealed class SwimmingConditionsHandler(
    IAlertRule<DomainForecast<MarinePoint>> rule,
    IOptions<AlertOptions> options) : IDomainHandler
{
    public string ConfigKey => "SwimmingConditions";

    public DomainRequirements Requirements { get; } = new(
        Marine: MarineParam.WaveHeight,
        OptionalMarine: MarineParam.WaterTemperature);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (!options.Value.Enabled || current.Marine is not { } marine)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var alert = rule.Evaluate(marine, options.Value, now);
        return alert.Severity == AlertSeverity.None ? [] : [new PipelineEvent.AlertRaised(current.Location, alert, now)];
    }
}
