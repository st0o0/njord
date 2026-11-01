using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SeasonalOptionsValidator : IValidateOptions<SeasonalOptions>
{
    public ValidateOptionsResult Validate(string? name, SeasonalOptions options)
    {
        if (options.ForecastMonths is < 1 or > 7)
        {
            return ValidateOptionsResult.Fail($"Seasonal.ForecastMonths must be between 1 and 7, got {options.ForecastMonths}");
        }

        return string.IsNullOrWhiteSpace(options.Model)
            ? ValidateOptionsResult.Fail("Seasonal.Model must not be empty")
            : ValidateOptionsResult.Success;
    }
}
