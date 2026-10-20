using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

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
    public const int WindowHours = 24;
    public const double MediumPeriodSeconds = 8.0;
    public const double LongPeriodSeconds = 12.0;
    public const double UsableHeightMinMeters = 0.5;
    public const double IdealHeightMinMeters = 1.0;
    public const double IdealHeightMaxMeters = 2.5;
    public const double UsableHeightMaxMeters = 3.5;
    public const double OffshoreToleranceDegrees = 45.0;
    public const double ChoppyShare = 0.5;
    public const double VeryChoppyShare = 0.75;
    public const int PoorScoreCap = 39;

    public const string Offshore = "offshore";
    public const string Cross = "cross";
    public const string Onshore = "onshore";

    public static SurfAssessment? Evaluate(
        DomainForecast<MarinePoint> marine, DomainForecast<WeatherPoint>? weather, DateTimeOffset now)
    {
        SurfAssessment? best = null;

        foreach (var point in marine.NextHours(now, WindowHours))
        {
            var assessment = Assess(point, WindDirectionAt(weather, point.ValidFrom));
            if (assessment is not null && (best is null || assessment.Score > best.Score))
            {
                best = assessment;
            }
        }

        return best;
    }

    public static SurfAssessment? Assess(MarinePoint point, double? windDirection)
    {
        if (point is not { SwellWaveHeight: { } height, SwellWavePeriod: { } period })
        {
            return null;
        }

        var periodPoints = period switch
        {
            < MediumPeriodSeconds => 10.0,
            <= LongPeriodSeconds => 30.0,
            _ => 50.0,
        };
        var heightPoints = HeightPoints(height);

        var relation = point.SwellWaveDirection is { } swellDirection && windDirection is { } wind
            ? Relation(wind, swellDirection)
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

        score -= ChoppyPenalty(point.WindWaveHeight, height);
        if (period < MediumPeriodSeconds)
        {
            score = Math.Min(score, PoorScoreCap);
        }

        var rounded = (int)Math.Round(Math.Clamp(score, 0.0, 100.0));
        return new SurfAssessment(rounded, RatingFor(rounded), height, period, relation, point.ValidFrom);
    }

    public static string Relation(double windFrom, double swellFrom)
    {
        var difference = Math.Abs(windFrom - swellFrom) % 360.0;
        if (difference > 180.0)
        {
            difference = 360.0 - difference;
        }

        if (difference >= 180.0 - OffshoreToleranceDegrees)
        {
            return Offshore;
        }

        return difference <= OffshoreToleranceDegrees ? Onshore : Cross;
    }

    public static string RatingFor(int score) => score switch
    {
        < 40 => "poor",
        < 60 => "fair",
        < 80 => "good",
        _ => "excellent",
    };

    private static double HeightPoints(double height)
    {
        if (height is >= IdealHeightMinMeters and <= IdealHeightMaxMeters)
        {
            return 30.0;
        }

        if (height is >= UsableHeightMinMeters and < IdealHeightMinMeters
            or > IdealHeightMaxMeters and <= UsableHeightMaxMeters)
        {
            return 20.0;
        }

        return 5.0;
    }

    private static double ChoppyPenalty(double? windWaveHeight, double swellHeight)
    {
        if (windWaveHeight is not { } windWave || swellHeight <= 0.0)
        {
            return 0.0;
        }

        var share = windWave / swellHeight;
        if (share > VeryChoppyShare)
        {
            return 20.0;
        }

        return share > ChoppyShare ? 10.0 : 0.0;
    }

    private static double? WindDirectionAt(DomainForecast<WeatherPoint>? weather, DateTimeOffset validFrom)
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
