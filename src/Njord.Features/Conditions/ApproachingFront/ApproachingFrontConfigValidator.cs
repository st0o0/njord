using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.ApproachingFront;

public sealed class ApproachingFrontConfigValidator : IValidateOptions<ApproachingFrontConfig>
{
    public ValidateOptionsResult Validate(string? name, ApproachingFrontConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<ApproachingFrontConfig>());

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");

        return failures.ToResult();
    }
}
