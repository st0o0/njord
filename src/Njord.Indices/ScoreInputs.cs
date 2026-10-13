using Njord.Weather;

namespace Njord.Indices;

public readonly record struct ScoreInputs(
    double? Temperature,
    double? Humidity,
    double? Wind,
    double? RainProbability,
    double? CloudCover,
    double? Radiation,
    double? Evapotranspiration,
    double? SunshinePercent)
{
    public static ScoreInputs From(IReadOnlyDictionary<ParameterDef, double?> means, double? sunshinePercent) => new(
        means.GetValueOrDefault(ParameterRegistry.Temperature2m),
        means.GetValueOrDefault(ParameterRegistry.RelativeHumidity2m),
        means.GetValueOrDefault(ParameterRegistry.WindSpeed10m),
        means.GetValueOrDefault(ParameterRegistry.PrecipitationProbability),
        means.GetValueOrDefault(ParameterRegistry.CloudCover),
        means.GetValueOrDefault(ParameterRegistry.ShortwaveRadiation),
        means.GetValueOrDefault(ParameterRegistry.Et0FaoEvapotranspiration),
        sunshinePercent);
}
