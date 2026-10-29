using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Results.Trends;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.PollenTrend;

public sealed class PollenTrend(IOptions<PollenTrendConfig> config) : FeatureRule<AqInput>
{
    public const double ChangeThreshold = 0.10;
    public const double MinimumChange = 1.0;

    private static readonly (PollenType Type, Func<AirQualityPoint, double?> Read)[] PollenTypes =
    [
        (PollenType.Birch, p => p.BirchPollen),
        (PollenType.Grass, p => p.GrassPollen),
        (PollenType.Alder, p => p.Alder),
        (PollenType.Mugwort, p => p.Mugwort),
    ];

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.AirQuality,
        Name: "PollenTrend",
        DefaultEnabled: false,
        new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
        | AirQualityParams.Alder | AirQualityParams.Mugwort),
        PollDepth: 1);

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } aq ? new(aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx)
    {
        if (ctx.History.Count < 2)
        {
            return null;
        }

        var currentPoints = ctx.Current.AirQuality.NextHours(ctx.Now);
        var previousPoints = ctx.History[1].AirQuality.NextHours(ctx.Now);

        var trends = new Dictionary<PollenType, PollenTrendDirection>();
        PollenType? peakType = null;
        DateTimeOffset? peakTime = null;
        var peakValue = 0.0;
        foreach (var (type, read) in PollenTypes)
        {
            var currentValues = currentPoints
                .Select(p => (p.ValidFrom, Value: read(p)))
                .Where(v => v.Value.HasValue)
                .Select(v => (v.ValidFrom, v.Value!.Value))
                .ToList();
            if (currentValues.Count == 0)
            {
                continue;
            }

            var previousValues = previousPoints.Select(read).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (previousValues.Count > 0)
            {
                trends[type] = Classify(currentValues.Average(v => v.Value), previousValues.Average());
            }

            var top = currentValues.MaxBy(v => v.Value);
            if (top.Value > peakValue)
            {
                peakValue = top.Value;
                peakType = type;
                peakTime = top.ValidFrom;
            }
        }

        if (trends.Count == 0)
        {
            return null;
        }

        return new PollenTrendResult(
            ctx.Location,
            peakType,
            peakTime,
            trends.GetValueOrDefault(PollenType.Birch),
            trends.GetValueOrDefault(PollenType.Grass),
            trends.GetValueOrDefault(PollenType.Alder),
            trends.GetValueOrDefault(PollenType.Mugwort));
    }

    private static PollenTrendDirection Classify(double currentAverage, double previousAverage)
    {
        var delta = currentAverage - previousAverage;
        if (Math.Abs(delta) < MinimumChange || Math.Abs(delta) < ChangeThreshold * previousAverage)
        {
            return PollenTrendDirection.Stable;
        }

        return delta > 0 ? PollenTrendDirection.Rising : PollenTrendDirection.Falling;
    }
}
