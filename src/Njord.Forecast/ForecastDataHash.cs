namespace Njord.Forecast;

public static class ForecastDataHash
{
    public static int Compute(Forecast forecast, TimeProvider timeProvider)
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
                if (value.Number.HasValue)
                {
                    hash.Add(value.Number.Value);
                }

                if (value.Text is not null)
                {
                    hash.Add(value.Text);
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
                hash.Add(value.HasValue);
                if (value.Number.HasValue)
                {
                    hash.Add(value.Number.Value);
                }

                if (value.Text is not null)
                {
                    hash.Add(value.Text);
                }
            }
        }

        return hash.ToHashCode();
    }
}
