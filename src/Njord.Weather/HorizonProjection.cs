namespace Njord.Weather;

public static class HorizonProjection
{
    public static Dictionary<Horizon, IReadOnlyList<HorizonValue>> BuildPerHorizon(
        Forecast forecast,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> horizons,
        int forecastDays,
        DateTimeOffset anchorTime,
        int? maxForecastHours = null)
    {
        var effectiveDays = EffectiveDays(forecastDays, maxForecastHours);
        var result = new Dictionary<Horizon, IReadOnlyList<HorizonValue>>(horizons.Count + effectiveDays);

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

            var values = ToHourlyValues(point, parameters);
            if (values.Count > 0)
            {
                result[Horizon.Hours(hours)] = values;
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

            var values = ToDailyValues(dailyPoint, parameters);
            if (values.Count > 0)
            {
                result[Horizon.Days(d)] = values;
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

    public static IReadOnlyList<HorizonValue> ToHourlyValues(ForecastPoint point, ResolvedParameterSet parameters)
    {
        var values = new List<HorizonValue>();
        foreach (var parameter in parameters.Hourly)
        {
            if (point.Get(parameter) is { } number)
            {
                values.Add(new HorizonValue(parameter, number, null));
            }
        }

        return values;
    }

    public static IReadOnlyList<HorizonValue> ToDailyValues(DailyForecastPoint point, ResolvedParameterSet parameters)
    {
        var values = new List<HorizonValue>();
        foreach (var parameter in parameters.Daily)
        {
            if (parameter.ValueType == ParameterValueType.TimeString)
            {
                if (point.GetMeta(parameter) is { } text)
                {
                    values.Add(new HorizonValue(parameter, null, text));
                }
            }
            else if (point.GetNumeric(parameter) is { } number)
            {
                values.Add(new HorizonValue(parameter, number, null));
            }
        }

        return values;
    }
}
