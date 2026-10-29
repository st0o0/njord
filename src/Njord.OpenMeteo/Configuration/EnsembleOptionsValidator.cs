using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class EnsembleOptionsValidator : IValidateOptions<EnsembleOptions>
{
    public ValidateOptionsResult Validate(string? name, EnsembleOptions options)
    {
        List<string> failures = [];

        if (options.ForecastDays is < 1 or > 35)
            failures.Add($"Ensemble.ForecastDays must be between 1 and 35, got {options.ForecastDays}");

        if (options.Models.Length == 0)
            failures.Add("Ensemble.Models must contain at least one model.");

        failures.AddRange(options.Models
            .GroupBy(m => m).Where(g => g.Count() > 1)
            .Select(g => $"Ensemble.Models contains '{g.Key}' more than once."));

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
