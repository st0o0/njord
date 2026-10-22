using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Forecasts.ModelComparison;

public sealed class ModelComparison(IOptions<ModelComparisonConfig> config) : FeatureRule<ModelComparisonInput>
{
    public const int ComparedDays = 5;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Forecast,
        FeatureCategory.Weather,
        Name: EffectiveWeatherModels.ComparisonFeatureName,
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.Precipitation));

    public override bool Enabled => config.Value.Enabled;

    protected override ModelComparisonInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Models.Count: >= 2 } ? new(w, forecast.Models) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ModelComparisonInput> ctx)
    {
        var today = DateOnly.FromDateTime(ctx.Now.UtcDateTime);
        var models = ctx.Current.Models.OrderBy(m => m.Key, StringComparer.Ordinal).ToList();

        var days = models
            .SelectMany(m => m.Value.Points.Select(p => (Model: m.Key, Point: p, Date: DateOnly.FromDateTime(p.ValidFrom.UtcDateTime))))
            .Where(x => x.Date >= today && x.Date < today.AddDays(ComparedDays))
            .GroupBy(x => x.Date)
            .OrderBy(g => g.Key)
            .Select(g => new ModelComparisonDay(
                g.Key,
                [.. g.GroupBy(x => x.Model).OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => Summarize(m.Key, m.Select(x => x.Point)))]))
            .ToImmutableArray();

        return days.IsEmpty ? null : new ModelComparisonResult(ctx.Location, days);
    }

    private static ModelDayValues Summarize(string modelId, IEnumerable<WeatherPoint> points)
    {
        var list = points.ToList();
        var temps = list.Where(p => p.Temperature is not null).Select(p => p.Temperature!.Value).ToList();
        var precip = list.Where(p => p.Precipitation is not null).Select(p => p.Precipitation!.Value).ToList();
        return new ModelDayValues(
            modelId,
            temps.Count > 0 ? Math.Round(temps.Max(), 1) : null,
            temps.Count > 0 ? Math.Round(temps.Min(), 1) : null,
            precip.Count > 0 ? Math.Round(precip.Sum(), 1) : null);
    }
}
