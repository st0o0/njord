using Njord.Configuration;
using Njord.Domain;

namespace Njord.Pipeline;

public sealed record WeightedTarget(LocationOptions Location, WeatherModel Model, int Weight)
{
    public static int ComputeWeight(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);
}
