using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.HeatPumpCop;

public sealed class HeatPumpCopCondition(IOptions<HeatPumpCopConfig> config) : FeatureRule<ForecastInput>
{

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "HeatPumpCop",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Temperature));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

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

        var cops = hours.Select(point => (point.ValidFrom, Cop: CopAt(options.TargetTemperature, point.Temperature!.Value))).ToList();
        var current = cops[0].Cop;
        var best = cops.MaxBy(entry => entry.Cop);
        return new HeatPumpCopConditionResult(
            ctx.Location,
            Math.Round(current, 2),
            Recommend(current),
            Math.Round(best.Cop, 2),
            best.ValidFrom.Hour);
    }

    public static double CopAt(double targetTemperature, double outsideTemperature) =>
        Math.Max(1.0, 6.81 - 0.121 * (targetTemperature - outsideTemperature));

    public static HeatPumpRecommendation Recommend(double cop) => cop switch
    {
        > 4.0 => HeatPumpRecommendation.Ideal,
        >= 2.5 => HeatPumpRecommendation.Normal,
        >= 1.5 => HeatPumpRecommendation.Inefficient,
        _ => HeatPumpRecommendation.Avoid,
    };
}
