using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.RagweedPollen;

public sealed class RagweedPollen(IOptions<RagweedPollenConfig> config) : FeatureRule<AqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.AirQuality,
        Name: "RagweedPollen",
        DefaultEnabled: false,
        new DomainRequirements(AirQuality: AirQualityParams.RagweedPollen));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } aq ? new(aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx)
    {
        var window = ctx.Current.AirQuality.NextHoursInclusive(ctx.Now);
        double? peak = null;
        foreach (var point in window)
        {
            if (point.RagweedPollen is { } v && (peak is null || v > peak))
            {
                peak = v;
            }
        }

        if (peak is null)
        {
            return null;
        }

        return new RagweedPollenResult(ctx.Location, Math.Round(peak.Value, 1));
    }
}
