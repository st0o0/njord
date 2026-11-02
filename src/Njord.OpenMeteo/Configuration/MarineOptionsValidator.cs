using Microsoft.Extensions.Options;

namespace Njord.OpenMeteo.Configuration;

public sealed class MarineOptionsValidator : IValidateOptions<MarineOptions>
{
    public ValidateOptionsResult Validate(string? name, MarineOptions options) =>
        options.ForecastDays is < 1 or > 16
            ? ValidateOptionsResult.Fail($"Marine.ForecastDays must be between 1 and 16, got {options.ForecastDays}")
            : ValidateOptionsResult.Success;
}
