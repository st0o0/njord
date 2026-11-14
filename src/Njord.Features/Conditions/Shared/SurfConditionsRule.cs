using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Features.Conditions.SurfConditions;

namespace Njord.Features.Conditions.Shared;

public sealed record SurfAssessment(
    int Score,
    string Rating,
    double SwellHeight,
    double SwellPeriod,
    string? WindRelation,
    DateTimeOffset ValidFrom);

public static class SurfConditionsRule
{
    public const string Offshore = "offshore";
    public const string Cross = "cross";
    public const string Onshore = "onshore";

    public static SurfAssessment? Evaluate(
        DomainForecast<MarinePoint> marine, DomainForecast<ForecastPoint>? weather, DateTimeOffset now,
        SurfConditionsConfig cfg)
    {
        SurfAssessment? best = null;

        foreach (var point in marine.NextHours(now, cfg.WindowHours))
        {
            var assessment = Assess(point, WindDirectionAt(weather, point.ValidFrom), cfg);
            if (assessment is not null && (best is null || assessment.Score > best.Score))
            {
                best = assessment;
            }
        }

        return best;
    }

    public static SurfAssessment? Assess(MarinePoint point, double? windDirection, SurfConditionsConfig cfg)
    {
        if (point is not { SwellWaveHeight: { } height, SwellWavePeriod: { } period })
        {
            return null;
        }

        var periodPoints = period < cfg.MediumPeriodSeconds ? 10.0
            : period <= cfg.LongPeriodSeconds ? 30.0
            : 50.0;
        var heightPoints = HeightPoints(height, cfg);

        var relation = point.SwellWaveDirection is { } swellDirection && windDirection is { } wind
            ? Relation(wind, swellDirection, cfg.OffshoreToleranceDegrees)
            : null;

        double score;
        if (relation is null)
        {
            score = (periodPoints + heightPoints) / 80.0 * 100.0;
        }
        else
        {
            score = periodPoints + heightPoints + relation switch
            {
                Offshore => 20.0,
                Cross => 10.0,
                _ => 0.0,
            };
        }

        score -= ChoppyPenalty(point.WindWaveHeight, height, cfg);
        if (period < cfg.MediumPeriodSeconds)
        {
            score = Math.Min(score, cfg.PoorScoreCap);
        }

        var rounded = (int)Math.Round(Math.Clamp(score, 0.0, 100.0));
        return new SurfAssessment(rounded, RatingFor(rounded), height, period, relation, point.ValidFrom);
    }

    public static string Relation(double windFrom, double swellFrom, double offshoreTolerance)
    {
        var difference = Math.Abs(windFrom - swellFrom) % 360.0;
        if (difference > 180.0)
        {
            difference = 360.0 - difference;
        }

        if (difference >= 180.0 - offshoreTolerance)
        {
            return Offshore;
        }

        return difference <= offshoreTolerance ? Onshore : Cross;
    }

    public static string RatingFor(int score) => score switch
    {
        < 40 => "poor",
        < 60 => "fair",
        < 80 => "good",
        _ => "excellent",
    };

    private static double HeightPoints(double height, SurfConditionsConfig cfg)
    {
        if (height >= cfg.IdealHeightMinMeters && height <= cfg.IdealHeightMaxMeters)
        {
            return 30.0;
        }

        if ((height >= cfg.UsableHeightMinMeters && height < cfg.IdealHeightMinMeters)
            || (height > cfg.IdealHeightMaxMeters && height <= cfg.UsableHeightMaxMeters))
        {
            return 20.0;
        }

        return 5.0;
    }

    private static double ChoppyPenalty(double? windWaveHeight, double swellHeight, SurfConditionsConfig cfg)
    {
        if (windWaveHeight is not { } windWave || swellHeight <= 0.0)
        {
            return 0.0;
        }

        var share = windWave / swellHeight;
        if (share > cfg.VeryChoppyShare)
        {
            return 20.0;
        }

        return share > cfg.ChoppyShare ? 10.0 : 0.0;
    }

    private static double? WindDirectionAt(DomainForecast<ForecastPoint>? weather, DateTimeOffset validFrom)
    {
        if (weather is null)
        {
            return null;
        }

        foreach (var point in weather.Points)
        {
            if (point.ValidFrom == validFrom)
            {
                return point.WindDirection;
            }
        }

        return null;
    }
}
