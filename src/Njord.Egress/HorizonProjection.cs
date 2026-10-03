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
        DateTimeOffset anchorTime,
        int? maxForecastHours = null)
    {
        var effectiveDays = maxForecastHours.HasValue
            ? Math.Min(forecastDays, (int)Math.Ceiling(maxForecastHours.Value / 24.0))
            : forecastDays;

        var result = new Dictionary<string, string>(horizons.Count + effectiveDays);

        var pointsByValidAt = forecast.Hourly.Points.ToDictionary(p => p.ValidAt);
        foreach (var hours in horizons)
        {
            if (maxForecastHours.HasValue && hours > maxForecastHours.Value)
            {
                continue;
            }

            var anchor = TimeAnchor.AtHorizon(anchorTime, hours);
            if (!pointsByValidAt.TryGetValue(anchor, out var point) || !point.HasAnyValue)
            {
                continue;
            }

            var entry = new JsonObject();
            foreach (var parameter in parameters.Hourly)
            {
                var value = point?.Get(parameter);
                if (value.HasValue)
                {
                    entry[parameter.JsonKey] = value.Value;
                }
            }

            if (entry.Count > 0)
            {
                result[$"h{hours}"] = entry.ToJsonString();
            }
        }

        var dailyByDate = forecast.Daily.Points.ToDictionary(p => p.Date);
        var today = DateOnly.FromDateTime(anchorTime.UtcDateTime);
        for (var d = 0; d < effectiveDays; d++)
        {
            var date = today.AddDays(d);
            if (!dailyByDate.TryGetValue(date, out var dailyPoint) || !dailyPoint.HasAnyValue)
            {
                continue;
            }

            var entry = new JsonObject();
            foreach (var parameter in parameters.Daily)
            {
                if (parameter.ValueType == ParameterValueType.TimeString)
                {
                    var str = dailyPoint?.GetMeta(parameter);
                    if (str is not null)
                    {
                        entry[parameter.JsonKey] = JsonValue.Create(str);
                    }
                }
                else
                {
                    var num = dailyPoint?.GetNumeric(parameter);
                    if (num.HasValue)
                    {
                        entry[parameter.JsonKey] = JsonValue.Create(num.Value);
                    }
                }
            }

            if (entry.Count > 0)
            {
                result[$"d{d}"] = entry.ToJsonString();
            }
        }

        return result;
    }
}
