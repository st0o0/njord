using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Njord.Forecast;
using Njord.Forecast.Forecast;
using Njord.OpenMeteo.Forecast;

namespace Njord.OpenMeteo.Ensemble;

internal static class EnsembleResponseMappingExtensions
{
    private const string TimeKey = "time";

    private static readonly FrozenDictionary<string, EnsembleApiVariable> SpreadNames =
        ApiVariableNames.For<EnsembleApiVariable>().ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

    private static readonly string[] BaseNames = [.. SpreadNames.Keys.OrderByDescending(n => n.Length)];

    public static DomainForecast<EnsemblePoint>? ToEnsembleForecast(
        this ForecastResponse response, DateTimeOffset modelRun, string primaryModel)
    {
        if (response.Hourly is not { } hourly || hourly.Count == 0)
        {
            return null;
        }

        if (!hourly.TryGetValue(TimeKey, out var timeElement))
        {
            return null;
        }

        var times = JsonSerializer.Deserialize<long[]>(timeElement.GetRawText()) ?? [];
        if (times.Length == 0)
        {
            return null;
        }

        var suffix = $"_{primaryModel}";
        double?[]? tempSpread = null;
        double?[]? precipSpread = null;
        double?[]? windSpread = null;

        foreach (var (key, value) in hourly)
        {
            if (key == TimeKey)
            {
                continue;
            }

            var baseName = ResolveBaseName(key, suffix);
            if (baseName is null)
            {
                continue;
            }

            if (!SpreadNames.TryGetValue(baseName, out var variable))
            {
                continue;
            }

            var values = JsonSerializer.Deserialize<double?[]>(value.GetRawText());
            switch (variable)
            {
                case EnsembleApiVariable.TemperatureSpread:
                    tempSpread = values;
                    break;
                case EnsembleApiVariable.PrecipitationSpread:
                    precipSpread = values;
                    break;
                case EnsembleApiVariable.WindSpeedSpread:
                    windSpread = values;
                    break;
            }
        }

        var points = new EnsemblePoint[times.Length];
        for (var i = 0; i < times.Length; i++)
        {
            points[i] = new EnsemblePoint(
                ValidFrom: DateTimeOffset.FromUnixTimeSeconds(times[i]),
                TemperatureSpread: At(tempSpread, i),
                PrecipitationSpread: At(precipSpread, i),
                WindSpeedSpread: At(windSpread, i));
        }

        return new DomainForecast<EnsemblePoint>(
            DomainKind.Ensemble, modelRun, TimeResolution.Hourly, [.. points]);
    }

    private static string? ResolveBaseName(string key, string modelSuffix)
    {
        foreach (var name in BaseNames)
        {
            if (key == name)
            {
                return name;
            }

            var suffixed = name + modelSuffix;
            if (key == suffixed)
            {
                return name;
            }
        }

        return null;
    }

    private static double? At(double?[]? series, int index) =>
        series is not null && index < series.Length ? series[index] : null;
}
