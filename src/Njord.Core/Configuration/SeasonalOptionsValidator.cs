using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class SeasonalOptionsValidator : IValidateOptions<SeasonalOptions>
{
    public ValidateOptionsResult Validate(string? name, SeasonalOptions options) =>
        options.ForecastMonths is < 1 or > 6
            ? ValidateOptionsResult.Fail("ForecastMonths must be between 1 and 6.")
            : ValidateOptionsResult.Success;
}
