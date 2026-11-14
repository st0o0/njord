using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.ApproachingFront;

public sealed class ApproachingFront(IOptions<ApproachingFrontConfig> config) : FeatureRule<ForecastInput>
{

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Insight,
        Name: "FrontApproaching",
        new DomainRequirements(Forecast: ForecastParams.Pressure | ForecastParams.Temperature | ForecastParams.WindDirection),
        PollDepth: 1,
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new ForecastInput(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        if (ctx.History.Count < 2)
        {
            return null;
        }

        var cfg = config.Value;
        var hours = ctx.Current.Forecast.NextHours(ctx.Now, 24 + cfg.WindowHours);
        var windows = hours
            .Select(start => (Start: start, End: hours.FirstOrDefault(p => p.ValidFrom == start.ValidFrom.AddHours(cfg.WindowHours))))
            .Where(pair => pair.End is not null)
            .Select(pair => (pair.Start, End: pair.End!, Drop: pair.Start.Pressure - pair.End!.Pressure ?? 0.0))
            .Where(window => window.Drop > cfg.PressureDropThresholdHpa && WindShifted(window.Start, window.End, cfg.WindShiftThresholdDegrees))
            .ToList();
        if (windows.Count == 0)
        {
            return null;
        }

        var (start, end, drop) = windows.MaxBy(window => window.Drop);
        var estimatedIn = start.ValidFrom - ctx.Now;
        return new ApproachingFrontResult(ctx.Location,
            (estimatedIn < TimeSpan.Zero ? TimeSpan.Zero : estimatedIn).TotalHours,
            Math.Round(drop, 1),
            Math.Round(start.Temperature - end.Temperature ?? 0.0, 1));
    }

    private static bool WindShifted(ForecastPoint start, ForecastPoint end, double windShiftThreshold)
    {
        if (start.WindDirection is not { } from || end.WindDirection is not { } to)
        {
            return true;
        }

        var difference = Math.Abs(to - from) % 360.0;
        return Math.Min(difference, 360.0 - difference) >= windShiftThreshold;
    }
}
