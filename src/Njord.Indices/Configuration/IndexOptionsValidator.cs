using Microsoft.Extensions.Options;
using Njord.Consensus;
using Njord.Messages.Indices;

namespace Njord.Indices.Configuration;

public sealed class IndexOptionsValidator : IValidateOptions<IndexOptions>
{
    public ValidateOptionsResult Validate(string? name, IndexOptions options)
    {
        var errors = new List<string>();

        ValidatePreferences(options.Preferences, "Indices.Preferences", errors);

        foreach (var (scoreName, prefs) in options.ScoreOverrides)
        {
            ValidateScoreName(scoreName, "Indices.ScoreOverrides", errors);

            ValidatePreferences(prefs, $"Indices.ScoreOverrides[{scoreName}]", errors);
        }

        foreach (var loc in options.LocationOverrides)
        {
            ValidatePreferences(loc.Preferences, $"Indices.LocationOverrides[{loc.Location}]", errors);
            foreach (var (scoreName, prefs) in loc.ScoreOverrides)
            {
                ValidateScoreName(scoreName, $"Indices.LocationOverrides[{loc.Location}].ScoreOverrides", errors);

                ValidatePreferences(prefs, $"Indices.LocationOverrides[{loc.Location}].ScoreOverrides[{scoreName}]", errors);
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateScoreName(string scoreName, string path, List<string> errors)
    {
        if (!IndexKinds.TryParse(scoreName, out _))
        {
            var known = string.Join(", ", IndexKinds.All.Select(k => k.ToConfigName()));
            errors.Add($"{path} contains unknown score name '{scoreName}'; expected one of: {known}");
        }
    }

    private static void ValidatePreferences(IndexPreferences prefs, string path, List<string> errors)
    {
        ValidateRange(prefs.HeatSensitivity, nameof(IndexPreferences.HeatSensitivity), path, 0.0, 5.0, errors);
        ValidateRange(prefs.HumiditySensitivity, nameof(IndexPreferences.HumiditySensitivity), path, 0.0, 5.0, errors);
        ValidateRange(prefs.WindSensitivity, nameof(IndexPreferences.WindSensitivity), path, 0.0, 5.0, errors);
        ValidateRange(prefs.RainSensitivity, nameof(IndexPreferences.RainSensitivity), path, 0.0, 5.0, errors);
    }

    private static void ValidateRange(double? value, string propertyName, string path, double min, double max, List<string> errors)
    {
        if (value is { } v && (v < min || v > max))
        {
            errors.Add($"{path}.{propertyName} must be between {min:0.0} and {max:0.0}, got {v}");
        }
    }
}
