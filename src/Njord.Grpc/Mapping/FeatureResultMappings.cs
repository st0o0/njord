using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class FeatureResultMappings
{
    public static V2.LifestyleUpdate ToProto(this FeatureResult result)
    {
        var proto = new V2.LifestyleUpdate { Type = result.WireKey, Output = result.Output.ToProto() };
        foreach (var (key, value) in result.ToFields())
        {
            proto.Data[key] = value;
        }

        return proto;
    }

    public static IReadOnlyDictionary<string, string> ToFields(this FeatureResult result) => result switch
    {
        ScoreResult score => FeatureData.Of(
            ("score", FeatureData.Number(score.Score)),
            ("best_start", FeatureData.Time(score.BestStart)),
            ("best_hours", FeatureData.Number(score.BestHours))),
        ConditionResult condition => With(condition.Data, ("state", condition.State)),
        TrendResult trend => With(trend.Data,
            ("direction", trend.Direction),
            ("magnitude", FeatureData.Number(trend.Magnitude))),
        ForecastResult forecast => ForecastFields(forecast),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.GetType().Name, "No field mapping for this result."),
    };

    private static Dictionary<string, string> With(IReadOnlyDictionary<string, string> data, params (string Key, string Value)[] extra)
    {
        var fields = new Dictionary<string, string>(data);
        foreach (var (key, value) in extra)
        {
            fields[key] = value;
        }

        return fields;
    }

    private static Dictionary<string, string> ForecastFields(ForecastResult forecast)
    {
        var fields = new Dictionary<string, string>();
        for (var i = 0; i < forecast.Entries.Length; i++)
        {
            var prefix = $"entry_{i + 1}";
            fields[prefix] = forecast.Entries[i].Label;
            foreach (var (key, value) in forecast.Entries[i].Values)
            {
                fields[$"{prefix}_{key}"] = value;
            }
        }

        return fields;
    }
}
