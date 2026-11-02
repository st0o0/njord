using Microsoft.Extensions.Options;

namespace Njord.OpenMeteo.Configuration;

public sealed class SeasonalOptionsValidator : IValidateOptions<SeasonalOptions>
{
    public ValidateOptionsResult Validate(string? name, SeasonalOptions options) =>
        options.ForecastMonths is < 1 or > 7
            ? ValidateOptionsResult.Fail($"Seasonal.ForecastMonths must be between 1 and 7, got {options.ForecastMonths}")
            : ValidateOptionsResult.Success;
}
