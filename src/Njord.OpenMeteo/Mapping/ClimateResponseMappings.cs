using Njord.Core.Domain;

namespace Njord.OpenMeteo.Mapping;

internal static class ClimateResponseMappings
{
    private const double SecondsPerHour = 3600.0;

    public static MonthlyBaseline[]? ToMonthlyBaselines(this ClimateResponse response)
    {
        if (response.Daily is not { } daily)
        {
            return null;
        }

        var baselines = new List<MonthlyBaseline>(12);
        for (var month = 1; month <= 12; month++)
        {
            var indices = Enumerable.Range(0, daily.Time.Length)
                .Where(i => DateTimeOffset.FromUnixTimeSeconds(daily.Time[i]).Month == month)
                .ToArray();
            var temperature = Mean(daily.TemperatureMean, indices);
            if (temperature is null)
            {
                return null;
            }

            var sunshine = Mean(daily.SunshineDuration, indices);
            baselines.Add(new MonthlyBaseline(
                month,
                temperature.Value,
                Mean(daily.PrecipitationSum, indices) ?? 0.0,
                sunshine is { } seconds ? seconds / SecondsPerHour : 0.0));
        }

        return [.. baselines];
    }

    private static double? Mean(double?[]? values, int[] indices)
    {
        if (values is null)
        {
            return null;
        }

        var present = indices.Where(i => i < values.Length && values[i].HasValue).Select(i => values[i]!.Value).ToArray();
        return present.Length == 0 ? null : present.Average();
    }
}
