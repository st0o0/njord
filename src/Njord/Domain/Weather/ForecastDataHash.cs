namespace Njord.Domain.Weather;

public static class ForecastDataHash
{
    public static int Compute(ModelForecast forecast, TimeProvider timeProvider)
    {
        var cutoff = timeProvider.GetUtcNow().Date.AddDays(1);
        var hash = new HashCode();

        foreach (var point in forecast.Hourly.Points)
        {
            if (point.ValidAt < cutoff)
            {
                continue;
            }

            foreach (var (param, value) in point.Values.OrderBy(kv => kv.Key.ApiName))
            {
                hash.Add(param.ApiName);
                hash.Add(value.HasValue);
                if (value.HasValue)
                {
                    hash.Add(value.Value);
                }
            }
        }

        var dailyCutoff = DateOnly.FromDateTime(cutoff);
        foreach (var point in forecast.Daily.Points)
        {
            if (point.Date < dailyCutoff)
            {
                continue;
            }

            foreach (var (param, value) in point.Values.OrderBy(kv => kv.Key.ApiName))
            {
                hash.Add(param.ApiName);
                hash.Add(value is not null);
                if (value is not null)
                {
                    hash.Add(value);
                }
            }
        }

        return hash.ToHashCode();
    }
}
