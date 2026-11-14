using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.SurfConditions;

public sealed class SurfConditionsConfigValidator : IValidateOptions<SurfConditionsConfig>
{
    public ValidateOptionsResult Validate(string? name, SurfConditionsConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<SurfConditionsConfig>());

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");
        failures.Require(options.MediumPeriodSeconds < options.LongPeriodSeconds,
            $"MediumPeriodSeconds must be less than LongPeriodSeconds, got {options.MediumPeriodSeconds} >= {options.LongPeriodSeconds}.");
        failures.Require(options.UsableHeightMinMeters < options.IdealHeightMinMeters,
            $"UsableHeightMinMeters must be less than IdealHeightMinMeters, got {options.UsableHeightMinMeters} >= {options.IdealHeightMinMeters}.");
        failures.Require(options.IdealHeightMinMeters < options.IdealHeightMaxMeters,
            $"IdealHeightMinMeters must be less than IdealHeightMaxMeters, got {options.IdealHeightMinMeters} >= {options.IdealHeightMaxMeters}.");
        failures.Require(options.IdealHeightMaxMeters < options.UsableHeightMaxMeters,
            $"IdealHeightMaxMeters must be less than UsableHeightMaxMeters, got {options.IdealHeightMaxMeters} >= {options.UsableHeightMaxMeters}.");
        failures.Require(options.OffshoreToleranceDegrees > 0,
            $"OffshoreToleranceDegrees must be positive, got {options.OffshoreToleranceDegrees}.");
        failures.Require(options.ChoppyShare < options.VeryChoppyShare,
            $"ChoppyShare must be less than VeryChoppyShare, got {options.ChoppyShare} >= {options.VeryChoppyShare}.");
        failures.Require(options.PoorScoreCap >= 0, $"PoorScoreCap must not be negative, got {options.PoorScoreCap}.");

        return failures.ToResult();
    }
}
