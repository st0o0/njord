using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class ForecastOptionsValidator : IValidateOptions<ForecastOptions>
{
    public ValidateOptionsResult Validate(string? name, ForecastOptions options)
    {
        List<string> failures = [];

        if (options.ForecastDays is < 1 or > 16)
            failures.Add($"Forecast.ForecastDays must be between 1 and 16, got {options.ForecastDays}");

        if (options.PastHours is < 0 or > 96)
            failures.Add($"Forecast.PastHours must be between 0 and 96, got {options.PastHours}");

        failures.AddRange(options.Models
            .GroupBy(m => m).Where(g => g.Count() > 1)
            .Select(g => $"Forecast.Models contains '{g.Key}' more than once."));

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
