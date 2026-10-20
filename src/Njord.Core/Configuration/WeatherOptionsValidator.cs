using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class WeatherOptionsValidator : IValidateOptions<WeatherOptions>
{
    public ValidateOptionsResult Validate(string? name, WeatherOptions options) =>
        options.ForecastDays is < 1 or > 16
            ? ValidateOptionsResult.Fail("ForecastDays must be between 1 and 16.")
            : ValidateOptionsResult.Success;
}
