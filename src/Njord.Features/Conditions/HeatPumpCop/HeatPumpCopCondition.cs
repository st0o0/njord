using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.HeatPumpCop;

public sealed class HeatPumpCopCondition(IOptions<HeatPumpCopConfig> config) : FeatureRule<ForecastInput>
{

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "HeatPumpCop",
        new DomainRequirements(Forecast: ForecastParams.Temperature),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new ForecastInput(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var options = config.Value;
        var hours = ForecastQueries.Upcoming(ctx.Current.Forecast, ctx.Now, options.ForecastHours)
            .Where(point => point.Temperature is not null)
            .ToList();
        if (hours.Count == 0 || hours[0].ValidFrom != ctx.Now.HourStart())
        {
            return null;
        }

        var cops = hours.Select(point => (point.ValidFrom, Cop: CopAt(options.TargetTemperature, point.Temperature!.Value, options.CopIntercept, options.CopSlope))).ToList();
        var current = cops[0].Cop;
        var (validFrom, cop) = cops.MaxBy(entry => entry.Cop);
        return new HeatPumpCopConditionResult(
            ctx.Location,
            Math.Round(current, 2),
            Recommend(current, options.IdealThreshold, options.NormalThreshold, options.InefficientThreshold),
            Math.Round(cop, 2),
            validFrom.Hour);
    }

    public static double CopAt(double targetTemperature, double outsideTemperature,
        double intercept = 6.81, double slope = 0.121) =>
        Math.Max(1.0, intercept - slope * (targetTemperature - outsideTemperature));

    public static HeatPumpRecommendation Recommend(double cop,
        double idealThreshold = 4.0, double normalThreshold = 2.5, double inefficientThreshold = 1.5) => cop switch
        {
            _ when cop > idealThreshold => HeatPumpRecommendation.Ideal,
            _ when cop >= normalThreshold => HeatPumpRecommendation.Normal,
            _ when cop >= inefficientThreshold => HeatPumpRecommendation.Inefficient,
            _ => HeatPumpRecommendation.Avoid,
        };
}
