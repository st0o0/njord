using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Forecasts.GrowingDegree;

public sealed class GrowingDegreeConfigValidator : IValidateOptions<GrowingDegreeConfig>
{
    public ValidateOptionsResult Validate(string? name, GrowingDegreeConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<GrowingDegreeConfig>());

        failures.Require(options.BaseTemperature is >= -10 and <= 30, $"BaseTemperature must be between -10 and 30, got {options.BaseTemperature}.");

        return failures.ToResult();
    }
}
