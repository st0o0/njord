using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Seasonal;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Forecasts;
using Njord.Messages.Stream;

namespace Njord.Features.Forecasts.SeasonalOutlook;

public sealed class SeasonalOutlook(IOptions<SeasonalOutlookConfig> config) : FeatureRule<SeasonalInput>
{
    public const int HighConfidenceMaxLead = 1;
    public const int MediumConfidenceMaxLead = 2;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Forecast,
        FeatureCategory.Insight,
        Name: "SeasonalOutlook",
        DefaultEnabled: false,
        new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly | SeasonalParams.PrecipitationAnomaly | SeasonalParams.PrecipitationMean));

    public override bool Enabled => config.Value.Enabled;

    protected override SeasonalInput? Project(LocationForecast forecast) =>
        forecast.Seasonal is { } s ? new(s) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<SeasonalInput> ctx)
    {
        var months = ctx.Current.Seasonal.Points
            .Select(point => (Point: point, Lead: (point.ValidFrom.Year - ctx.Now.Year) * 12 + point.ValidFrom.Month - ctx.Now.Month))
            .Where(m => m.Lead > 0 && m.Point.TemperatureAnomaly is not null)
            .OrderBy(m => m.Point.ValidFrom)
            .Select(m => new SeasonalMonth(
                new DateOnly(m.Point.ValidFrom.Year, m.Point.ValidFrom.Month, 1),
                Math.Round(m.Point.TemperatureAnomaly!.Value, 1),
                PrecipitationPercent(m.Point),
                ConfidenceForLead(m.Lead)))
            .ToImmutableArray();
        return months.IsEmpty ? null : new SeasonalOutlookResult(ctx.Location, months);
    }

    public static ConfidenceLevel ConfidenceForLead(int leadMonths) => leadMonths switch
    {
        <= HighConfidenceMaxLead => ConfidenceLevel.High,
        <= MediumConfidenceMaxLead => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.Low,
    };

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
