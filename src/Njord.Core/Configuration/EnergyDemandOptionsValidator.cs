using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class EnergyDemandOptionsValidator : IValidateOptions<EnergyDemandOptions>
{
    public ValidateOptionsResult Validate(string? name, EnergyDemandOptions options) =>
        options.HeatingSetpoint > options.CoolingSetpoint
            ? ValidateOptionsResult.Fail(
                $"EnergyDemand.HeatingSetpoint ({options.HeatingSetpoint}) must not exceed CoolingSetpoint ({options.CoolingSetpoint})")
            : ValidateOptionsResult.Success;
}
