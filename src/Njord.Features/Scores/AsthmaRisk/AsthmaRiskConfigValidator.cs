using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Scores.AsthmaRisk;

public sealed class AsthmaRiskConfigValidator : IValidateOptions<AsthmaRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, AsthmaRiskConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<AsthmaRiskConfig>());

        failures.Require(options.Pm25Weight >= 0, $"Pm25Weight must not be negative, got {options.Pm25Weight}.");
        failures.Require(options.OzoneWeight >= 0, $"OzoneWeight must not be negative, got {options.OzoneWeight}.");
        failures.Require(options.PollenWeight >= 0, $"PollenWeight must not be negative, got {options.PollenWeight}.");
        failures.Require(options.HumidityWeight >= 0, $"HumidityWeight must not be negative, got {options.HumidityWeight}.");
        failures.Require(options.BoundaryLayerWeight >= 0, $"BoundaryLayerWeight must not be negative, got {options.BoundaryLayerWeight}.");

        var sum = options.Pm25Weight + options.OzoneWeight + options.PollenWeight
                  + options.HumidityWeight + options.BoundaryLayerWeight;
        failures.Require(sum == 100, $"weights must sum to 100, got {sum}.");

        failures.Require(options.MinWindowScore >= 1, $"MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        failures.Require(options.MinWindowHours >= 1, $"MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        failures.Require(options.MaxWindows >= 1, $"MaxWindows must be at least 1, got {options.MaxWindows}.");
        failures.Require(options.Pm25Reference > 0, $"Pm25Reference must be positive, got {options.Pm25Reference}.");
        failures.Require(options.OzoneReference > 0, $"OzoneReference must be positive, got {options.OzoneReference}.");
        failures.Require(options.PollenReference > 0, $"PollenReference must be positive, got {options.PollenReference}.");
        failures.Require(options.GoodHourScore > 0, $"GoodHourScore must be positive, got {options.GoodHourScore}.");
        failures.Require(options.ColdAirFactor >= 1.0, $"ColdAirFactor must be at least 1.0, got {options.ColdAirFactor}.");
        failures.Require(options.RapidChangeFactor >= 1.0, $"RapidChangeFactor must be at least 1.0, got {options.RapidChangeFactor}.");
        failures.Require(options.RapidChangeDegrees > 0, $"RapidChangeDegrees must be positive, got {options.RapidChangeDegrees}.");
        failures.Require(options.RapidChangeHours >= 1, $"RapidChangeHours must be at least 1, got {options.RapidChangeHours}.");

        return failures.ToResult();
    }
}
