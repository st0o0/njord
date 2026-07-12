using System.Text.Json.Nodes;
using Njord.Domain;

namespace Njord.Egress;

public static class StatePayloadBuilder
{
    public static Dictionary<string, string> BuildPerHorizon(
        ModelForecast forecast,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> horizons,
        int forecastDays,
        DateTimeOffset anchorTime)
    {
        var result = new Dictionary<string, string>(horizons.Count + forecastDays);

        var pointsByValidAt = forecast.Hourly.Points.ToDictionary(p => p.ValidAt);
        foreach (var hours in horizons)
        {
            var anchor = Anchor(anchorTime, hours);
            pointsByValidAt.TryGetValue(anchor, out var point);

            var entry = new JsonObject();
            foreach (var parameter in parameters.Hourly)
            {
                entry[parameter.JsonKey] = point?.Get(parameter);
            }

            result[$"h{hours}"] = entry.ToJsonString();
        }

        var dailyByDate = forecast.Daily.Points.ToDictionary(p => p.Date);
        var today = DateOnly.FromDateTime(anchorTime.UtcDateTime);
        for (var d = 0; d < forecastDays; d++)
        {
            var date = today.AddDays(d);
            dailyByDate.TryGetValue(date, out var dailyPoint);

            var entry = new JsonObject();
            foreach (var parameter in parameters.Daily)
            {
                var value = dailyPoint?.Get(parameter);
                entry[parameter.JsonKey] = value switch
                {
                    double num => JsonValue.Create(num),
                    string str => JsonValue.Create(str),
                    _ => null,
                };
            }

            result[$"d{d}"] = entry.ToJsonString();
        }

        return result;
    }

    public static DateTimeOffset Anchor(DateTimeOffset tick, int horizonHours)
    {
        var target = tick.AddHours(horizonHours);
        var floored = new DateTimeOffset(target.Year, target.Month, target.Day, target.Hour, 0, 0, target.Offset);
        return floored == target ? target : floored.AddHours(1);
    }
}
