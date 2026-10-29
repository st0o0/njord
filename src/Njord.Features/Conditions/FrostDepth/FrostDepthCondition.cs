using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.FrostDepth;

public sealed class FrostDepthCondition(IOptions<FrostDepthConfig> config) : FeatureRule<ForecastInput>
{

    public const int ForecastHours = 48;
    public const double StefanAlpha = 2.4;
    public const double PipeApproachFraction = 0.8;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "FrostDepth",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.SoilTemperature
            | ForecastParams.SnowDepth | ForecastParams.WindSpeed));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

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
        var depth = DepthCm(degreeHours, snowCm, windSpeed);
        var soilTemperature = hours[0].SoilTemperature ?? hours[0].Temperature!.Value;

        return new FrostDepthConditionResult(
            ctx.Location,
            Math.Round(depth, 1),
            RiskFor(depth, config.Value.PipeDepthCm),
            Math.Round(snowCm, 1),
            Math.Round(soilTemperature, 1));
    }

    public static double DepthCm(double freezingDegreeHours, double snowDepthCm, double windSpeed)
    {
        if (freezingDegreeHours <= 0.0)
        {
            return 0.0;
        }

        var wind = Math.Min(1.5, 1.0 + 0.1 * Math.Max(0.0, windSpeed - 5.0));
        var freezingDegreeDays = freezingDegreeHours / 24.0;
        return StefanAlpha * Math.Sqrt(freezingDegreeDays) / (1.0 + snowDepthCm / 10.0) * wind;
    }

    public static FrostDepthRisk RiskFor(double depthCm, double pipeDepthCm) => depthCm switch
    {
        <= 0.0 => FrostDepthRisk.None,
        _ when pipeDepthCm > 0.0 && depthCm >= pipeDepthCm * PipeApproachFraction => FrostDepthRisk.Deep,
        < 5.0 => FrostDepthRisk.Surface,
        < 15.0 => FrostDepthRisk.Shallow,
        < 30.0 => FrostDepthRisk.Moderate,
        _ => FrostDepthRisk.Deep,
    };
}
