using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class AlertOptionsValidator : IValidateOptions<AlertOptions>
{
    public ValidateOptionsResult Validate(string? name, AlertOptions options)
    {
        var errors = new List<string>();

        Descending(options.FrostThresholds, nameof(AlertOptions.FrostThresholds), errors);
        Ascending(options.HeatThresholds, nameof(AlertOptions.HeatThresholds), errors);
        Ascending(options.StormGustThresholds, nameof(AlertOptions.StormGustThresholds), errors);
        Descending(options.WindChillThresholds, nameof(AlertOptions.WindChillThresholds), errors);
        Descending(options.VisibilityThresholds, nameof(AlertOptions.VisibilityThresholds), errors);
        Ascending(options.TropicalNightThresholds, nameof(AlertOptions.TropicalNightThresholds), errors);
        Ascending(options.HumidityThresholds, nameof(AlertOptions.HumidityThresholds), errors);

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void Ascending(double[] values, string property, List<string> errors)
    {
        if (HasThreeValues(values, property, errors) && !(values[0] <= values[1] && values[1] <= values[2]))
        {
            errors.Add($"Alerts.{property} must rise from advisory to severe (e.g. [30, 35, 40]), got [{string.Join(", ", values)}]");
        }
    }

    private static void Descending(double[] values, string property, List<string> errors)
    {
        if (HasThreeValues(values, property, errors) && !(values[0] >= values[1] && values[1] >= values[2]))
        {
            errors.Add($"Alerts.{property} must fall from advisory to severe (e.g. [0, -5, -15]), got [{string.Join(", ", values)}]");
        }
    }

    private static bool HasThreeValues(double[] values, string property, List<string> errors)
    {
        if (values.Length == 3)
        {
            return true;
        }

        errors.Add($"Alerts.{property} needs exactly three values (advisory, warning, severe), got {values.Length}");
        return false;
    }
}
