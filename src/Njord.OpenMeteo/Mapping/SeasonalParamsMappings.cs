using Njord.Forecast.Seasonal;

namespace Njord.OpenMeteo.Mapping;

public static class SeasonalParamsMappings
{
    public static IReadOnlyList<string> ToApiNames(this SeasonalParams flags) =>
        [.. Enum.GetValues<SeasonalParams>().Where(p => p != SeasonalParams.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(SeasonalParams param) => param switch
    {
        SeasonalParams.TemperatureAnomaly => "temperature_2m_anomaly",
        SeasonalParams.PrecipitationAnomaly => "precipitation_anomaly",
        SeasonalParams.PrecipitationMean => "precipitation_mean",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
