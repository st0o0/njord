using Njord.Core;

namespace Njord.Messages.Results.Scores;

public sealed record BbqScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record SprayWindowScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record LaundryScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record AllergySeverityScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record SleepQualityScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record AsthmaRiskScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record OutdoorScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record SurfConditionsResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);
