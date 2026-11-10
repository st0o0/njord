using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.AtmosphericStability;

public sealed class AtmosphericStabilityCondition(IOptions<AtmosphericStabilityConfig> config)
    : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Insight,
        Name: "AtmosphericStability",
        new DomainRequirements(Forecast: ForecastParams.Cape | ForecastParams.ConvectiveInhibition |
                                         ForecastParams.LiftedIndex),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new ForecastInput(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var point = ForecastQueries.Upcoming(ctx.Current.Forecast, ctx.Now, 1).FirstOrDefault();
        if (point is not { Cape: { } cape } || point.ValidFrom != ctx.Now.HourStart())
        {
            return null;
        }

        var cin = point.ConvectiveInhibition ?? 0.0;
        var lifted = point.LiftedIndex ?? 0.0;
        return new AtmosphericStabilityConditionResult(ctx.Location, Classify(cape, cin, lifted), cape, cin, lifted);
    }

    public static StabilityClass Classify(double cape, double cin, double liftedIndex)
    {
        return cape switch
        {
            < 300 => StabilityClass.Stable,
            < 1000 => StabilityClass.Marginal,
            > 200 => StabilityClass.Capped,
            _ => cin switch
            {
                < 50 when cape >= 2500 && liftedIndex < -6 => StabilityClass.Explosive,
                < 50 when liftedIndex < -3 => StabilityClass.Unstable,
                _ => StabilityClass.Marginal
            }
        };
    }
}
