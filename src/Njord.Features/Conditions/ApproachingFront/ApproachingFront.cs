using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.ApproachingFront;

public sealed class ApproachingFront(IOptions<ApproachingFrontConfig> config) : FeatureRule<WeatherInput>
{
    public const double PressureDropThreshold = 3.0;
    public const double WindShiftThreshold = 45.0;
    public const int WindowHours = 6;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Insight,
        Name: "FrontApproaching",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Pressure | WeatherParams.Temperature | WeatherParams.WindDirection),
        PollDepth: 1);

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        if (ctx.History.Count < 2)
        {
            return null;
        }

        var hours = ctx.Current.Weather.NextHours(ctx.Now, 24 + WindowHours);
        var windows = hours
            .Select(start => (Start: start, End: hours.FirstOrDefault(p => p.ValidFrom == start.ValidFrom.AddHours(WindowHours))))
            .Where(pair => pair.End is not null)
            .Select(pair => (pair.Start, End: pair.End!, Drop: (pair.Start.Pressure - pair.End!.Pressure) ?? 0.0))
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
            Math.Round((best.Start.Temperature - best.End.Temperature) ?? 0.0, 1));
    }

    private static bool WindShifted(WeatherPoint start, WeatherPoint end)
    {
        if (start.WindDirection is not { } from || end.WindDirection is not { } to)
        {
            return true;
        }

        var difference = Math.Abs(to - from) % 360.0;
        return Math.Min(difference, 360.0 - difference) >= WindShiftThreshold;
    }
}
