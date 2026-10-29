using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.CompoundHeatStress;

public sealed class CompoundHeatStressConfigValidator : IValidateOptions<CompoundHeatStressConfig>
{
    public ValidateOptionsResult Validate(string? name, CompoundHeatStressConfig options) =>
        options.WetBulbWarning < options.WetBulbSevere && options.WetBulbSevere < options.WetBulbExtreme
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{IConfigSection.SectionOf<CompoundHeatStressConfig>()}: WetBulbWarning < WetBulbSevere < WetBulbExtreme required, got {options.WetBulbWarning}, {options.WetBulbSevere}, {options.WetBulbExtreme}.");
}
