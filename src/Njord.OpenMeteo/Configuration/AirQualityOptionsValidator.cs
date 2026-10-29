using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class AirQualityOptionsValidator : IValidateOptions<AirQualityOptions>
{
    public ValidateOptionsResult Validate(string? name, AirQualityOptions options)
    {
        List<string> failures = [];

        if (options.ForecastDays is < 1 or > 7)
            failures.Add($"AirQuality.ForecastDays must be between 1 and 7, got {options.ForecastDays}");

        if (!Enum.IsDefined(options.Region))
            failures.Add($"AirQuality.Region '{options.Region}' is not a valid AirQualityRegion.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
