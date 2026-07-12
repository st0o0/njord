using System.Text.Json.Nodes;
using Njord.Domain;

namespace Njord.Egress;

public static class StatePayloadBuilder
{
    public static string Build(
        ModelForecast forecast,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> horizons,
        int forecastDays)
    {
        var root = new JsonObject();

        var pointsByValidAt = forecast.Hourly.Points.ToDictionary(p => p.ValidAt);
        foreach (var hours in horizons)
        {
            var anchor = Anchor(forecast.Cycle.Timestamp, hours);
            pointsByValidAt.TryGetValue(anchor, out var point);

            var entry = new JsonObject { ["valid_at"] = anchor.ToString("O") };
            foreach (var parameter in parameters.Hourly)
            {
                entry[parameter.JsonKey] = point?.Get(parameter);
            }

            root[$"h{hours}"] = entry;
        }

        var dailyByDate = forecast.Daily.Points.ToDictionary(p => p.Date);
        var today = DateOnly.FromDateTime(forecast.Cycle.Timestamp.UtcDateTime);
        for (var d = 0; d < forecastDays; d++)
        {
            var date = today.AddDays(d);
            dailyByDate.TryGetValue(date, out var dailyPoint);

            var entry = new JsonObject { ["date"] = date.ToString("O") };
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

            root[$"d{d}"] = entry;
        }

        return root.ToJsonString();
    }

    public static DateTimeOffset Anchor(DateTimeOffset tick, int horizonHours)
    {
        var target = tick.AddHours(horizonHours);
        var floored = new DateTimeOffset(target.Year, target.Month, target.Day, target.Hour, 0, 0, target.Offset);
        return floored == target ? target : floored.AddHours(1);
    }
}
