using System.Globalization;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Weather.Lifestyle;

public sealed class ClimateAnomalyHandler(IClimateBaselineStore baselines) : IDomainHandler
{
    public string ConfigKey => "ClimateAnomaly";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Weather };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather
            || !baselines.Baselines.TryGetValue(current.Location.Name, out var months))
        {
            return [];
        }

        var now = time.GetUtcNow();
        var baseline = months.FirstOrDefault(month => month.Month == now.Month);
        var hours = ForecastQueries.Upcoming(weather, now, 24);
        if (baseline is null || hours.Count == 0)
        {
            return [];
        }

        var tempAnomaly = hours.Average(point => point.Temperature) - baseline.MeanTemp;
        // Units match: baseline is mean daily precipitation sum (mm/day), compared with the next 24 h sum (mm).
        var precipAnomaly = baseline.MeanPrecip > 0.0
            ? (hours.Sum(point => point.Precipitation) - baseline.MeanPrecip) / baseline.MeanPrecip * 100.0
            : 0.0;
        var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(now.Month);

        return [new PipelineEvent.ClimateAnomalyComputed(
            current.Location,
            tempAnomaly,
            precipAnomaly,
            $"1991-2020 {monthName} average")];
    }
}
