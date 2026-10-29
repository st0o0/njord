using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Forecasts.GrowingDegree;

public sealed class GrowingDegreeConfigValidator : IValidateOptions<GrowingDegreeConfig>
{
    public ValidateOptionsResult Validate(string? name, GrowingDegreeConfig options)
    {
        var section = IConfigSection.SectionOf<GrowingDegreeConfig>();
        var failures = new List<string>();

        if (options.BaseTemperature is < -10 or > 30)
        {
            failures.Add($"{section}:BaseTemperature must be between -10 and 30, got {options.BaseTemperature}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
