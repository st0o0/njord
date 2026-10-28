using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.CloudCeiling;

public sealed class CloudCeilingCondition(IOptions<CloudCeilingConfig> config) : FeatureRule<ForecastInput>
{
    public const double HighCloudCeilingM = 8000.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Insight,
        Name: "CloudCeiling",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.CloudCoverLow | ForecastParams.CloudCoverMid | ForecastParams.CloudCoverHigh
            | ForecastParams.FreezingLevelHeight | ForecastParams.BoundaryLayerHeight));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var point = ForecastQueries.Upcoming(ctx.Current.Forecast, ctx.Now, 1).FirstOrDefault();
        if (point is not { CloudCoverLow: { } low, CloudCoverMid: { } mid, CloudCoverHigh: { } high }
            || point.ValidFrom != ctx.Now.HourStart())
        {
            return null;
        }

        double? ceiling;
        if (low > 50)
        {
            ceiling = point.BoundaryLayerHeight;
        }
        else if (mid > 50)
        {
            ceiling = point.FreezingLevelHeight;
        }
        else if (high > 50)
        {
            ceiling = HighCloudCeilingM;
        }
        else
        {
            return new CloudCeilingConditionResult(ctx.Location, CeilingCategory.Unlimited, 0, (int)Math.Round(low), (int)Math.Round(mid), (int)Math.Round(high));
        }

        if (ceiling is not { } meters)
        {
            return null;
        }

        return new CloudCeilingConditionResult(
            ctx.Location,
            Categorize(meters),
            (int)Math.Round(meters),
            (int)Math.Round(low),
            (int)Math.Round(mid),
            (int)Math.Round(high));
    }

    public static CeilingCategory Categorize(double meters) => meters switch
    {
        < 300 => CeilingCategory.VeryLow,
        < 1000 => CeilingCategory.Low,
        < 3000 => CeilingCategory.Moderate,
        <= 6000 => CeilingCategory.High,
        _ => CeilingCategory.VeryHigh,
    };
}
