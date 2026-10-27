using Njord.Core.Flood;

namespace Njord.OpenMeteo.Flood;

public static class FloodApiVariableMappingExtensions
{
    public static FloodApiVariable ToApiVariable(this FloodParams param) => param switch
    {
        FloodParams.Discharge => FloodApiVariable.Discharge,
        FloodParams.DischargeMean => FloodApiVariable.DischargeMean,
        FloodParams.DischargeMedian => FloodApiVariable.DischargeMedian,
        FloodParams.DischargeMax => FloodApiVariable.DischargeMax,
        FloodParams.DischargeMin => FloodApiVariable.DischargeMin,
        FloodParams.DischargeP25 => FloodApiVariable.DischargeP25,
        FloodParams.DischargeP75 => FloodApiVariable.DischargeP75,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<FloodApiVariable> ToApiVariables(this FloodParams flags) =>
        [.. Enum.GetValues<FloodParams>().Where(p => p is not (FloodParams.None or FloodParams.Ensemble) && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this FloodParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
