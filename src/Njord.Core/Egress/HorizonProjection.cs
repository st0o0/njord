using System.Text.Json.Nodes;
using Njord.Weather;

namespace Njord.Core.Egress;

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
        var effectiveDays = EffectiveDays(forecastDays, maxForecastHours);
        var result = new Dictionary<string, string>(horizons.Count + effectiveDays);

        var pointsByValidAt = forecast.Hourly.Points.ToDictionary(p => p.ValidAt);
        foreach (var hours in horizons)
        {
            if (!IsHorizonApplicable(hours, maxForecastHours))
            {
                continue;
            }

            var anchor = TimeAnchor.AtHorizon(anchorTime, hours);
            if (!pointsByValidAt.TryGetValue(anchor, out var point) || !point.HasAnyValue)
            {
                continue;
            }

            var payload = ToHourlyPayload(point, parameters);
            if (payload is not null)
            {
                result[$"h{hours}"] = payload;
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

            var payload = ToDailyPayload(dailyPoint, parameters);
            if (payload is not null)
            {
                result[$"d{d}"] = payload;
            }
        }

        return result;
    }

    public static int EffectiveDays(int forecastDays, int? maxForecastHours) =>
        maxForecastHours.HasValue
            ? Math.Min(forecastDays, (int)Math.Ceiling(maxForecastHours.Value / 24.0))
            : forecastDays;

    public static bool IsHorizonApplicable(int hours, int? maxForecastHours) =>
        !maxForecastHours.HasValue || hours <= maxForecastHours.Value;

    public static string? ToHourlyPayload(ForecastPoint point, ResolvedParameterSet parameters)
    {
        var entry = new JsonObject();
        foreach (var parameter in parameters.Hourly)
        {
            var value = point.Get(parameter);
            if (value.HasValue)
            {
                entry[parameter.JsonKey] = value.Value;
            }
        }

        return entry.Count > 0 ? entry.ToJsonString() : null;
    }

    public static string? ToDailyPayload(DailyForecastPoint point, ResolvedParameterSet parameters)
    {
        var entry = new JsonObject();
        foreach (var parameter in parameters.Daily)
        {
            if (parameter.ValueType == ParameterValueType.TimeString)
            {
                var str = point.GetMeta(parameter);
                if (str is not null)
                {
                    entry[parameter.JsonKey] = JsonValue.Create(str);
                }
            }
            else
            {
                var num = point.GetNumeric(parameter);
                if (num.HasValue)
                {
                    entry[parameter.JsonKey] = JsonValue.Create(num.Value);
                }
            }
        }

        return entry.Count > 0 ? entry.ToJsonString() : null;
    }
}
