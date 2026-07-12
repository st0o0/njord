using System.Text.Json.Nodes;
using Njord.Domain;

namespace Njord.Egress;

/// <summary>Builds the retained per-device state JSON: one object per horizon, keyed "h&lt;hours&gt;".</summary>
public static class StatePayloadBuilder
{
    public static string Build(ModelForecast forecast, IReadOnlyList<int> horizons)
    {
        var pointsByValidAt = forecast.Series.Points.ToDictionary(p => p.ValidAt);
        var root = new JsonObject();

        foreach (var hours in horizons)
        {
            var anchor = Anchor(forecast.Cycle.Timestamp, hours);
            pointsByValidAt.TryGetValue(anchor, out var point);

            var entry = new JsonObject { ["valid_at"] = anchor.ToString("O") };
            foreach (var parameter in Enum.GetValues<WeatherParameter>())
            {
                entry[parameter.JsonKey()] = point?.Get(parameter);
            }

            root[$"h{hours}"] = entry;
        }

        return root.ToJsonString();
    }

    /// <summary>Next full grid hour at or after tick + horizon — a horizon sensor never points into the past.</summary>
    public static DateTimeOffset Anchor(DateTimeOffset tick, int horizonHours)
    {
        var target = tick.AddHours(horizonHours);
        var floored = new DateTimeOffset(target.Year, target.Month, target.Day, target.Hour, 0, 0, target.Offset);
        return floored == target ? target : floored.AddHours(1);
    }
}
