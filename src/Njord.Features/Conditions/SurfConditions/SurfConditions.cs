using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Features.Conditions.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Scores;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.SurfConditions;

public sealed class SurfConditions(IOptions<SurfConditionsConfig> config) : FeatureRule<MarineForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Marine,
        Name: "Surf",
        DefaultEnabled: false,
        new DomainRequirements(
        Marine: MarineParams.SwellWaveHeight | MarineParams.SwellWavePeriod,
        OptionalMarine: MarineParams.SwellWaveDirection | MarineParams.WindWaveHeight,
        OptionalForecast: ForecastParams.WindDirection));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineForecastInput? Project(LocationForecast forecast) =>
        forecast.Marine is { } m ? new(m, forecast.Forecast!) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineForecastInput> ctx)
    {
        if (SurfConditionsRule.Evaluate(ctx.Current.Marine, ctx.Current.Forecast, ctx.Now) is not { } surf)
        {
            return null;
        }

        return new SurfConditionsResult(ctx.Location, surf.Score, surf.ValidFrom, 0);
    }
}
