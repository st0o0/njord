using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class AirQualityOptionsValidator : IValidateOptions<AirQualityOptions>
{
    public ValidateOptionsResult Validate(string? name, AirQualityOptions options)
    {
        var errors = new List<string>();

        if (options.ForecastDays is < 1 or > 7)
        {
            errors.Add("ForecastDays must be between 1 and 7.");
        }

        if (!Enum.IsDefined(options.Region))
        {
            errors.Add($"Region '{options.Region}' is not a valid AirQualityRegion.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
