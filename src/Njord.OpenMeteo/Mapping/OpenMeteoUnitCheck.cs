using Njord.Weather;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoUnitCheck
{
    public static string? Mismatch(Dictionary<string, string>? units, ResolvedParameterSet parameters)
    {
        if (units is null || units.Count == 0)
        {
            return "Response carried no hourly_units";
        }

        if (units.TryGetValue("time", out var timeUnit) && timeUnit != "unixtime")
        {
            return $"Unexpected unit '{timeUnit}' for time (expected 'unixtime')";
        }

        foreach (var param in parameters.Hourly)
        {
            var expected = param switch
            {
                { Unit: "°C" } => "°C",
                { Unit: "m/s" } => "m/s",
                { Unit: "hPa" } => "hPa",
                _ => null,
            };

            if (expected is null)
            {
                continue;
            }

            if (units.TryGetValue(param.ApiName, out var actual) && actual != expected && actual != "undefined")
            {
                return $"Unexpected unit '{actual}' for {param.ApiName} (expected '{expected}')";
            }
        }

        return null;
    }
}
