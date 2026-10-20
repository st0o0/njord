using Njord.Forecast.Flood;

namespace Njord.OpenMeteo.Mapping;

public static class FloodParamsMappingExtensions
{
    public static IReadOnlyList<string> ToApiNames(this FloodParams flags) =>
        [.. Enum.GetValues<FloodParams>().Where(p => p is not (FloodParams.None or FloodParams.Ensemble) && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(FloodParams param) => param switch
    {
        FloodParams.Discharge => "river_discharge",
        FloodParams.DischargeMean => "river_discharge_mean",
        FloodParams.DischargeMedian => "river_discharge_median",
        FloodParams.DischargeMax => "river_discharge_max",
        FloodParams.DischargeMin => "river_discharge_min",
        FloodParams.DischargeP25 => "river_discharge_p25",
        FloodParams.DischargeP75 => "river_discharge_p75",
        FloodParams.Ensemble => throw new ArgumentOutOfRangeException(nameof(param), param, "Ensemble is a request flag, not a daily variable."),
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
