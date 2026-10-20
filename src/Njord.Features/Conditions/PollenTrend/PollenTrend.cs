using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.PollenTrend;

public sealed class PollenTrend(IOptions<PollenTrendConfig> config) : FeatureRule<AqInput>
{
    public const double ChangeThreshold = 0.10;
    public const double MinimumChange = 1.0;

    private static readonly (string Name, Func<AirQualityPoint, double?> Read)[] PollenTypes =
    [
        ("birch", p => p.BirchPollen),
        ("grass", p => p.GrassPollen),
        ("alder", p => p.Alder),
        ("mugwort", p => p.Mugwort),
    ];

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.AirQuality,
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

        var trends = new Dictionary<string, string>();
        string? peakType = null;
        DateTimeOffset? peakTime = null;
        var peakValue = 0.0;
        foreach (var (name, read) in PollenTypes)
        {
            var currentValues = currentPoints
                .Select(p => (p.ValidFrom, Value: read(p)))
                .Where(v => v.Value.HasValue)
                .Select(v => (v.ValidFrom, Value: v.Value!.Value))
                .ToList();
            if (currentValues.Count == 0)
            {
                continue;
            }

            var previousValues = previousPoints.Select(read).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (previousValues.Count > 0)
            {
                trends[name] = Classify(currentValues.Average(v => v.Value), previousValues.Average());
            }

            var top = currentValues.MaxBy(v => v.Value);
            if (top.Value > peakValue)
            {
                peakValue = top.Value;
                peakType = name;
                peakTime = top.ValidFrom;
            }
        }

        if (trends.Count == 0)
        {
            return null;
        }

        return new PollenTrendResult(ctx.Location, peakType ?? "none", 0.0, Fields(trends, peakTime));
    }

    private static IReadOnlyDictionary<string, string> Fields(Dictionary<string, string> trends, DateTimeOffset? peakTime)
    {
        var fields = new Dictionary<string, string>();
        foreach (var (pollen, trend) in trends.OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            fields[$"trend_{pollen}"] = trend;
        }

        if (peakTime is { } time)
        {
            fields["peak_time"] = FeatureData.Time(time);
        }

        return fields;
    }

    private static string Classify(double currentAverage, double previousAverage)
    {
        var delta = currentAverage - previousAverage;
        if (Math.Abs(delta) < MinimumChange || Math.Abs(delta) < ChangeThreshold * previousAverage)
        {
            return "stable";
        }

        return delta > 0 ? "rising" : "falling";
    }
}
