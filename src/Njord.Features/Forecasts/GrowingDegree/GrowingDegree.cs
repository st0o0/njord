using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Features.Forecasts.GrowingDegree;

public sealed class GrowingDegree(IOptions<GrowingDegreeConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Forecast,
        FeatureCategory.Lifestyle,
        Name: "GrowingDegree",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Temperature));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w } ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var baseTemp = config.Value.BaseTemperature;
        var today = DateOnly.FromDateTime(ctx.Now.UtcDateTime);
        var points = ctx.Current.Forecast.Points;

        var days = points
            .Where(p => p.Temperature is not null)
            .GroupBy(p => DateOnly.FromDateTime(p.ValidFrom.UtcDateTime))
            .Where(g => g.Key >= today)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var temps = g.Select(p => p.Temperature!.Value).ToList();
                var mean = (temps.Max() + temps.Min()) / 2.0;
                var gdd = Math.Max(mean - baseTemp, 0);
                return new GrowingDegreeDay(g.Key, Math.Round(gdd, 1));
            })
            .ToImmutableArray();

        if (days.IsEmpty)
        {
            return null;
        }

        var cumulative = Math.Round(days.Sum(d => d.Gdd), 1);
        return new GrowingDegreeResult(ctx.Location, days, cumulative);
    }
}
