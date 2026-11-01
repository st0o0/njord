using System.Collections.Immutable;
using Njord.Core.Forecast;
using Njord.Messages.Results.Forecasts;

namespace Njord.Features.Forecasts.Shared;

internal static class ModelComparisonHelper
{
    internal const int ComparedDays = 5;

    internal static ImmutableArray<ModelComparisonDay> CompareDays(
        ImmutableDictionary<string, DomainForecast<ForecastPoint>> models, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var ordered = models.OrderBy(m => m.Key, StringComparer.Ordinal).ToList();

        var days = ordered
            .SelectMany(m => m.Value.Points.Select(p => (Model: m.Key, Point: p, Date: DateOnly.FromDateTime(p.ValidFrom.UtcDateTime))))
            .Where(x => x.Date >= today && x.Date < today.AddDays(ComparedDays))
            .GroupBy(x => x.Date)
            .OrderBy(g => g.Key)
            .Select(g => new ModelComparisonDay(
                g.Key,
                [.. g.GroupBy(x => x.Model).OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => Summarize(m.Key, m.Select(x => x.Point)))]))
            .ToImmutableArray();

        return days;
    }

    private static ModelDayValues Summarize(string modelId, IEnumerable<ForecastPoint> points)
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
