using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.IrrigationNeed;

public sealed class IrrigationNeedCondition(IOptions<IrrigationNeedConfig> config) : FeatureRule<ForecastInput>
{

    public const double HeavyRainMm = 5.0;
    public const double HighProbabilityPercent = 60.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "IrrigationNeed",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Evapotranspiration | ForecastParams.SoilMoisture0To1cm
            | ForecastParams.VapourPressureDeficit | ForecastParams.Rain | ForecastParams.PrecipitationProbability
            | ForecastParams.Temperature));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var hours = ctx.Current.Forecast.NextHours(ctx.Now);
        if (hours.Count == 0 || hours[0].SoilMoisture0To1cm is not { } moisture)
        {
            return null;
        }

        var options = config.Value;
        var moisturePercent = moisture * 100.0;
        var etMm = hours.Sum(point => point.Evapotranspiration ?? 0.0);
        var rainMm = hours.Sum(point => point.Rain ?? 0.0);
        var expectedRainMm = hours.Sum(point => (point.Rain ?? 0.0) * (point.PrecipitationProbability ?? 100.0) / 100.0);
        var rainyHours = hours.Where(point => point.Rain is > 0.0).ToList();
        var averageProbability = rainyHours.Count == 0
            ? 0.0
            : rainyHours.Average(point => point.PrecipitationProbability ?? 100.0);

        var advice = Advise(moisturePercent, etMm, rainMm, expectedRainMm, averageProbability, options);
        return new IrrigationNeedConditionResult(
            ctx.Location,
            advice,
            Math.Round(moisturePercent, 1),
            Math.Round(expectedRainMm, 2),
            Math.Round(etMm, 2));
    }

    private static IrrigationAdvice Advise(double moisturePercent, double etMm, double rainMm, double expectedRainMm, double averageProbability, IrrigationNeedConfig options)
    {
        if (rainMm > HeavyRainMm && averageProbability > HighProbabilityPercent)
        {
            return IrrigationAdvice.Skip;
        }

        if (moisturePercent > options.WetThresholdPercent)
        {
            return IrrigationAdvice.SoilWet;
        }

        if (etMm > expectedRainMm && moisturePercent < options.DryThresholdPercent)
        {
            return IrrigationAdvice.Water;
        }

        return expectedRainMm > 0.0 ? IrrigationAdvice.Reduce : IrrigationAdvice.Skip;
    }
}
