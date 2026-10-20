using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class FloodOptionsValidator : IValidateOptions<FloodOptions>
{
    public ValidateOptionsResult Validate(string? name, FloodOptions options) =>
        options.ForecastDays is < 1 or > 210
            ? ValidateOptionsResult.Fail("ForecastDays must be between 1 and 210.")
            : ValidateOptionsResult.Success;
}
