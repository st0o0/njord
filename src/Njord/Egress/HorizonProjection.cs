using System.Text.Json.Nodes;
using Njord.Domain.Weather;

namespace Njord.Egress;

public static class HorizonProjection
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
            var anchor = TimeAnchor.AtHorizon(anchorTime, hours);
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
                if (parameter.ValueType == ParameterValueType.TimeString)
                {
                    var str = dailyPoint?.GetMeta(parameter);
                    entry[parameter.JsonKey] = str is not null ? JsonValue.Create(str) : null;
                }
                else
                {
                    var num = dailyPoint?.GetNumeric(parameter);
                    entry[parameter.JsonKey] = num.HasValue ? JsonValue.Create(num.Value) : null;
                }
            }

            result[$"d{d}"] = entry.ToJsonString();
        }

        return result;
    }
}
