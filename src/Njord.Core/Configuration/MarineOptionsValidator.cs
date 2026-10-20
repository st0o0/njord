using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class MarineOptionsValidator : IValidateOptions<MarineOptions>
{
    public ValidateOptionsResult Validate(string? name, MarineOptions options) =>
        options.ForecastDays is < 1 or > 8
            ? ValidateOptionsResult.Fail("ForecastDays must be between 1 and 8.")
            : ValidateOptionsResult.Success;
}
