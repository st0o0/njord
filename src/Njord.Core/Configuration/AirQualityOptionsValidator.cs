using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class AirQualityOptionsValidator : IValidateOptions<AirQualityOptions>
{
    public ValidateOptionsResult Validate(string? name, AirQualityOptions options) =>
        options.ForecastDays is < 1 or > 7
            ? ValidateOptionsResult.Fail("ForecastDays must be between 1 and 7.")
            : ValidateOptionsResult.Success;
}
