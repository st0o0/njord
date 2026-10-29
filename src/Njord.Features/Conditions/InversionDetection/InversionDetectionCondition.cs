using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.InversionDetection;

public sealed class InversionDetectionCondition(IOptions<InversionDetectionConfig> config) : FeatureRule<ForecastInput>
{

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Insight,
        Name: "InversionDetection",
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.Temperature80m
            | ForecastParams.BoundaryLayerHeight | ForecastParams.CloudCover | ForecastParams.WindSpeed,
            OptionalForecast: ForecastParams.Temperature120m | ForecastParams.Visibility));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var point = ForecastQueries.Upcoming(ctx.Current.Forecast, ctx.Now, 1).FirstOrDefault();
        if (point is not { Temperature: { } surface, Temperature80m: { } t80, BoundaryLayerHeight: { } layer }
            || point.ValidFrom != ctx.Now.HourStart())
        {
            return null;
        }

        var diff = t80 - surface;
        var strength = ClassifyStrength(diff, layer);
        if (strength == InversionStrength.None)
        {
            return null;
        }

        return new InversionDetectionConditionResult(
            ctx.Location,
            strength,
            ClassifyType(diff, layer, point.CloudCover, point.WindSpeed),
            Math.Round(diff, 1),
            Math.Round(layer));
    }

    public static InversionStrength ClassifyStrength(double tempDiff, double boundaryLayer) => (tempDiff, boundaryLayer) switch
    {
        ( <= 0, _) => InversionStrength.None,
        (_, >= 800) => InversionStrength.None,
        ( > 4, < 300) => InversionStrength.Extreme,
        ( > 2, < 500) => InversionStrength.Strong,
        _ => InversionStrength.Mild,
    };

    public static InversionType ClassifyType(double tempDiff, double boundaryLayer, double? cloudCover, double? windSpeed)
    {
        if (cloudCover < 30 && windSpeed < 3)
        {
            return InversionType.Radiation;
        }

        return boundaryLayer < 500 && tempDiff > 2 ? InversionType.Subsidence : InversionType.Frontal;
    }
}
