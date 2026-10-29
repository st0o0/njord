using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.ApproachingFront;

public sealed class ApproachingFront(IOptions<ApproachingFrontConfig> config) : FeatureRule<ForecastInput>
{
    public const double PressureDropThreshold = 3.0;
    public const double WindShiftThreshold = 45.0;
    public const int WindowHours = 6;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Insight,
        Name: "FrontApproaching",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Pressure | ForecastParams.Temperature | ForecastParams.WindDirection),
        PollDepth: 1);

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        if (ctx.History.Count < 2)
        {
            return null;
        }

        var hours = ctx.Current.Forecast.NextHours(ctx.Now, 24 + WindowHours);
        var windows = hours
            .Select(start => (Start: start, End: hours.FirstOrDefault(p => p.ValidFrom == start.ValidFrom.AddHours(WindowHours))))
            .Where(pair => pair.End is not null)
            .Select(pair => (pair.Start, End: pair.End!, Drop: pair.Start.Pressure - pair.End!.Pressure ?? 0.0))
            .Where(window => window.Drop > PressureDropThreshold && WindShifted(window.Start, window.End))
            .ToList();
        if (windows.Count == 0)
        {
            return null;
        }

        var best = windows.MaxBy(window => window.Drop);
        var estimatedIn = best.Start.ValidFrom - ctx.Now;
        return new ApproachingFrontResult(ctx.Location,
            (estimatedIn < TimeSpan.Zero ? TimeSpan.Zero : estimatedIn).TotalHours,
            Math.Round(best.Drop, 1),
            Math.Round(best.Start.Temperature - best.End.Temperature ?? 0.0, 1));
    }

    private static bool WindShifted(ForecastPoint start, ForecastPoint end)
    {
        if (start.WindDirection is not { } from || end.WindDirection is not { } to)
        {
            return true;
        }

        var difference = Math.Abs(to - from) % 360.0;
        return Math.Min(difference, 360.0 - difference) >= WindShiftThreshold;
    }
}
