using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.InversionDetection;

public sealed class InversionDetectionCondition(IOptions<InversionDetectionConfig> config) : FeatureRule<ForecastInput>
{
    public const string None = "none";
    public const string Mild = "mild";
    public const string Strong = "strong";
    public const string Extreme = "extreme";
    public const string Radiation = "radiation";
    public const string Subsidence = "subsidence";
    public const string Frontal = "frontal";

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Insight,
        Name: "InversionDetection",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.Temperature80m | ForecastParams.Temperature120m
            | ForecastParams.BoundaryLayerHeight | ForecastParams.Visibility | ForecastParams.CloudCover | ForecastParams.WindSpeed));

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
        if (strength == None)
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

    public static string ClassifyStrength(double tempDiff, double boundaryLayer) => (tempDiff, boundaryLayer) switch
    {
        ( <= 0, _) => None,
        (_, >= 800) => None,
        ( > 4, < 300) => Extreme,
        ( > 2, < 500) => Strong,
        _ => Mild,
    };

    public static string ClassifyType(double tempDiff, double boundaryLayer, double? cloudCover, double? windSpeed)
    {
        if (cloudCover < 30 && windSpeed < 3)
        {
            return Radiation;
        }

        return boundaryLayer < 500 && tempDiff > 2 ? Subsidence : Frontal;
    }
}
