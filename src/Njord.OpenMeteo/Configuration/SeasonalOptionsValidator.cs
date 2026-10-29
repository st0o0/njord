using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SeasonalOptionsValidator : IValidateOptions<SeasonalOptions>
{
    public ValidateOptionsResult Validate(string? name, SeasonalOptions options) =>
        options.ForecastMonths is < 1 or > 6
            ? ValidateOptionsResult.Fail($"Seasonal.ForecastMonths must be between 1 and 6, got {options.ForecastMonths}")
            : ValidateOptionsResult.Success;
}
