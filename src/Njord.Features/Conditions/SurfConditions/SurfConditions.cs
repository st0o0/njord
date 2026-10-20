using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.Shared;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.SurfConditions;

public sealed class SurfConditions(IOptions<SurfConditionsConfig> config) : FeatureRule<MarineWeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Marine,
        DefaultEnabled: false,
        new DomainRequirements(
        Marine: MarineParams.SwellWaveHeight | MarineParams.SwellWavePeriod,
        OptionalMarine: MarineParams.SwellWaveDirection | MarineParams.WindWaveHeight,
        OptionalWeather: WeatherParams.WindDirection));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineWeatherInput? Project(LocationForecast forecast) =>
        forecast.Marine is { } m ? new(m, forecast.Weather!) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineWeatherInput> ctx)
    {
        if (SurfConditionsRule.Evaluate(ctx.Current.Marine, ctx.Current.Weather, ctx.Now) is not { } surf)
        {
            return null;
        }

        return new SurfConditionsResult(ctx.Location, surf.Score, surf.ValidFrom, 0);
    }
}
