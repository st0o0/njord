using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Marine;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.OceanCurrents;

public sealed class OceanCurrents(IOptions<OceanCurrentsConfig> config) : FeatureRule<MarineInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Marine,
        Name: "OceanCurrents",
        DefaultEnabled: false,
        new DomainRequirements(Marine: MarineParams.OceanCurrentVelocity | MarineParams.OceanCurrentDirection));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineInput? Project(LocationForecast forecast) =>
        forecast.Marine is { } m ? new(m) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineInput> ctx)
    {
        var window = ctx.Current.Marine.NextHoursInclusive(ctx.Now);
        MarinePoint? strongest = null;
        var peakVelocity = 0.0;

        foreach (var point in window)
        {
            if (point.OceanCurrentVelocity is { } v && v > peakVelocity)
            {
                peakVelocity = v;
                strongest = point;
            }
        }

        if (strongest is null)
        {
            return null;
        }

        var cfg = config.Value;
        var strength = peakVelocity switch
        {
            _ when peakVelocity >= cfg.StrongCurrentThresholdKmh * 2 => CurrentStrength.VeryStrong,
            _ when peakVelocity >= cfg.StrongCurrentThresholdKmh => CurrentStrength.Strong,
            _ when peakVelocity >= cfg.StrongCurrentThresholdKmh * 0.5 => CurrentStrength.Moderate,
            _ => CurrentStrength.Weak,
        };

        return new OceanCurrentsResult(
            ctx.Location,
            strength,
            Math.Round(peakVelocity, 1),
            strongest.OceanCurrentDirection);
    }
}
