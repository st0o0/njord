using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.FrostDepth;

public sealed class FrostDepthCondition(IOptions<FrostDepthConfig> config) : FeatureRule<ForecastInput>
{

    public const int ForecastHours = 48;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "FrostDepth",
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.SoilTemperature
            | ForecastParams.SnowDepth | ForecastParams.WindSpeed),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new ForecastInput(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var hours = ctx.Current.Forecast.NextHours(ctx.Now, ForecastHours);
        if (hours.Count == 0 || hours[0].Temperature is null)
        {
            return null;
        }

        var freezing = hours.Where(point => point.Temperature is < 0.0).ToList();
        var degreeHours = freezing.Sum(point => -point.Temperature!.Value);
        var snowCm = (hours[0].SnowDepth ?? 0.0) * 100.0;
        var windSpeed = freezing.Count == 0 ? 0.0 : freezing.Average(point => point.WindSpeed ?? 0.0);
        var cfg = config.Value;
        var depth = DepthCm(degreeHours, snowCm, windSpeed, cfg.StefanAlpha);
        var soilTemperature = hours[0].SoilTemperature ?? hours[0].Temperature!.Value;

        return new FrostDepthConditionResult(
            ctx.Location,
            Math.Round(depth, 1),
            RiskFor(depth, cfg.PipeDepthCm, cfg.PipeApproachFraction, cfg.SurfaceThresholdCm, cfg.ShallowThresholdCm, cfg.ModerateThresholdCm),
            Math.Round(snowCm, 1),
            Math.Round(soilTemperature, 1));
    }

    public static double DepthCm(double freezingDegreeHours, double snowDepthCm, double windSpeed, double alpha = 2.4)
    {
        if (freezingDegreeHours <= 0.0)
        {
            return 0.0;
        }

        var wind = Math.Min(1.5, 1.0 + 0.1 * Math.Max(0.0, windSpeed - 5.0));
        var freezingDegreeDays = freezingDegreeHours / 24.0;
        return alpha * Math.Sqrt(freezingDegreeDays) / (1.0 + snowDepthCm / 10.0) * wind;
    }

    public static FrostDepthRisk RiskFor(
        double depthCm, double pipeDepthCm,
        double pipeApproachFraction = 0.8,
        double surfaceThresholdCm = 5.0, double shallowThresholdCm = 15.0, double moderateThresholdCm = 30.0) => depthCm switch
        {
            <= 0.0 => FrostDepthRisk.None,
            _ when pipeDepthCm > 0.0 && depthCm >= pipeDepthCm * pipeApproachFraction => FrostDepthRisk.Deep,
            _ when depthCm < surfaceThresholdCm => FrostDepthRisk.Surface,
            _ when depthCm < shallowThresholdCm => FrostDepthRisk.Shallow,
            _ when depthCm < moderateThresholdCm => FrostDepthRisk.Moderate,
            _ => FrostDepthRisk.Deep,
        };
}
