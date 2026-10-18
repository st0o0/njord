using System.Text.Json;
using Njord.Weather;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoSeriesMappings
{
    public static ForecastSeries? ToForecastSeries(this OpenMeteoTimeSeries series, ResolvedParameterSet parameters)
    {
        var times = series.Time;
        var paramArrays = new Dictionary<ParameterDef, IReadOnlyList<double?>>(parameters.Hourly.Count);

        foreach (var param in parameters.Hourly)
        {
            paramArrays[param] = series.Variables.TryGetValue(param.ApiName, out var element)
                ? ParseNumberArray(element, times.Count)
                : new double?[times.Count];
        }

        var points = new List<ForecastPoint>(times.Count);
        for (var i = 0; i < times.Count; i++)
        {
            var dict = new Dictionary<ParameterDef, double?>(parameters.Hourly.Count);
            foreach (var param in parameters.Hourly)
            {
                dict[param] = paramArrays[param][i];
            }

            points.Add(new ForecastPoint(DateTimeOffset.FromUnixTimeSeconds(times[i]), dict));
        }

        var lastValued = points.FindLastIndex(p => p.HasAnyValue);
        return lastValued < 0 ? null : new ForecastSeries(points.Take(lastValued + 1));
    }

    public static DailyForecastSeries ToDailySeries(this OpenMeteoTimeSeries series, ResolvedParameterSet parameters)
    {
        var times = series.Time;
        var paramArrays = new Dictionary<ParameterDef, IReadOnlyList<object?>>(parameters.Daily.Count);

        foreach (var param in parameters.Daily)
        {
            paramArrays[param] = series.Variables.TryGetValue(param.ApiName, out var element)
                ? ParseMixedArray(element, times.Count)
                : new object?[times.Count];
        }

        var points = new List<DailyForecastPoint>(times.Count);
        for (var i = 0; i < times.Count; i++)
        {
            var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(times[i]).UtcDateTime);
            var numeric = new Dictionary<ParameterDef, double?>();
            var meta = new Dictionary<ParameterDef, string?>();

            foreach (var param in parameters.Daily)
            {
                var raw = i < paramArrays[param].Count ? paramArrays[param][i] : null;
                if (param.ValueType == ParameterValueType.TimeString)
                {
                    meta[param] = raw switch
                    {
                        string s => s,
                        double d => DateTimeOffset.FromUnixTimeSeconds((long)d).ToString("O"),
                        _ => null,
                    };
                }
                else
                {
                    numeric[param] = raw as double?;
                }
            }

            var point = new DailyForecastPoint(date, numeric, meta);
            if (point.HasAnyValue)
            {
                points.Add(point);
            }
        }

        return new DailyForecastSeries(points);
    }

    private static List<double?> ParseNumberArray(JsonElement element, int expectedLength)
    {
        var result = new List<double?>(expectedLength);
        foreach (var item in element.EnumerateArray())
        {
            result.Add(item.ValueKind == JsonValueKind.Null ? null : item.GetDouble());
        }

        return result;
    }

    private static List<object?> ParseMixedArray(JsonElement element, int expectedLength)
    {
        var result = new List<object?>(expectedLength);
        foreach (var item in element.EnumerateArray())
        {
            result.Add(item.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number => item.GetDouble(),
                JsonValueKind.String => item.GetString(),
                _ => null,
            });
        }

        return result;
    }
}
