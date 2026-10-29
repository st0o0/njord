using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.AtmosphericStability;

public sealed class AtmosphericStabilityCondition(IOptions<AtmosphericStabilityConfig> config) : FeatureRule<ForecastInput>
{

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Insight,
        Name: "AtmosphericStability",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Cape | ForecastParams.ConvectiveInhibition | ForecastParams.LiftedIndex));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

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
        if (cape < 300)
        {
            return StabilityClass.Stable;
        }

        if (cape < 1000)
        {
            return StabilityClass.Marginal;
        }

        if (cin > 200)
        {
            return StabilityClass.Capped;
        }

        if (cin < 50)
        {
            if (cape >= 2500 && liftedIndex < -6)
            {
                return StabilityClass.Explosive;
            }

            if (liftedIndex < -3)
            {
                return StabilityClass.Unstable;
            }
        }

        return StabilityClass.Marginal;
    }
}
