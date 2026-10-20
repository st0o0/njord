using System.Collections.Immutable;
using System.Globalization;
using Njord.Core.Domain;
using Njord.Forecast.Seasonal;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class SeasonalOutlookHandler : IDomainHandler
{
    public const int HighConfidenceMaxLead = 1;

    public const int MediumConfidenceMaxLead = 2;

    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.SeasonalOutlook,
        FeatureCategory.Insight,
        "Monthly temperature and precipitation outlook for the coming months",
        DefaultEnabled: false,
        new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly | SeasonalParams.PrecipitationAnomaly | SeasonalParams.PrecipitationMean));

    // The seasonal forecast is an ensemble mean without member spread, so confidence follows the lead time:
    // seasonal skill falls with every month ahead.
    public static OutlookConfidence ConfidenceForLead(int leadMonths) => leadMonths switch
    {
        <= HighConfidenceMaxLead => OutlookConfidence.High,
        <= MediumConfidenceMaxLead => OutlookConfidence.Medium,
        _ => OutlookConfidence.Low,
    };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Seasonal is not { } seasonal)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var months = seasonal.Points
            .Select(point => (Point: point, Lead: (point.ValidFrom.Year - now.Year) * 12 + point.ValidFrom.Month - now.Month))
            .Where(m => m.Lead > 0 && m.Point.TemperatureAnomaly is not null)
            .OrderBy(m => m.Point.ValidFrom)
            .Select(m => new MonthlyOutlook(
                m.Point.ValidFrom.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Math.Round(m.Point.TemperatureAnomaly!.Value, 1),
                PrecipitationPercent(m.Point),
                ConfidenceForLead(m.Lead)))
            .ToImmutableArray();
        return months.IsEmpty ? [] : [new PipelineEvent.SeasonalOutlookComputed(current.Location, months)];
    }

    private static double PrecipitationPercent(SeasonalPoint point)
    {
        if (point is not { PrecipitationAnomaly: { } anomaly, PrecipitationMean: { } mean })
        {
            return 0;
        }

        var normal = mean - anomaly;
        return normal > 0 ? Math.Round(anomaly / normal * 100, 1) : 0;
    }
}
