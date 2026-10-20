using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Seasonal;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Forecasts.SeasonalOutlook;

public sealed class SeasonalOutlook(IOptions<SeasonalOutlookConfig> config) : FeatureRule<SeasonalWeatherInput>
{
    public const int HighConfidenceMaxLead = 1;
    public const int MediumConfidenceMaxLead = 2;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Forecast,
        FeatureCategory.Insight,
        DefaultEnabled: false,
        new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly | SeasonalParams.PrecipitationAnomaly | SeasonalParams.PrecipitationMean));

    public override bool Enabled => config.Value.Enabled;

    protected override SeasonalWeatherInput? Project(LocationForecast forecast) =>
        forecast is { Seasonal: { } s, Weather: { } w } ? new(s, w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<SeasonalWeatherInput> ctx)
    {
        var months = ctx.Current.Seasonal.Points
            .Select(point => (Point: point, Lead: (point.ValidFrom.Year - ctx.Now.Year) * 12 + point.ValidFrom.Month - ctx.Now.Month))
            .Where(m => m.Lead > 0 && m.Point.TemperatureAnomaly is not null)
            .OrderBy(m => m.Point.ValidFrom)
            .Select(m => new MonthlyOutlook(
                m.Point.ValidFrom.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Math.Round(m.Point.TemperatureAnomaly!.Value, 1),
                PrecipitationPercent(m.Point),
                ConfidenceForLead(m.Lead)))
            .ToImmutableArray();
        return months.IsEmpty ? null : new SeasonalOutlookResult(ctx.Location, [.. months.Select(Entry)]);
    }

    public static ConfidenceLevel ConfidenceForLead(int leadMonths) => leadMonths switch
    {
        <= HighConfidenceMaxLead => ConfidenceLevel.High,
        <= MediumConfidenceMaxLead => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.Low,
    };

    private static ForecastEntry Entry(MonthlyOutlook month) => new(month.Month, FeatureData.Of(
        ("temp_anomaly", FeatureData.Number(month.TempAnomaly)),
        ("precip_anomaly", FeatureData.Number(month.PrecipAnomaly)),
        ("confidence", month.Confidence.ToWireName())));

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

internal sealed record MonthlyOutlook(string Month, double TempAnomaly, double PrecipAnomaly, ConfidenceLevel Confidence);
