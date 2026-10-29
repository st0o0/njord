using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class MarineOptionsValidator : IValidateOptions<MarineOptions>
{
    public ValidateOptionsResult Validate(string? name, MarineOptions options) =>
        options.ForecastDays is < 1 or > 8
            ? ValidateOptionsResult.Fail($"Marine.ForecastDays must be between 1 and 8, got {options.ForecastDays}")
            : ValidateOptionsResult.Success;
}
