using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Flood;

public enum FloodApiVariable
{
    [JsonStringEnumMemberName("river_discharge")]
    Discharge,
    [JsonStringEnumMemberName("river_discharge_mean")]
    DischargeMean,
    [JsonStringEnumMemberName("river_discharge_median")]
    DischargeMedian,
    [JsonStringEnumMemberName("river_discharge_max")]
    DischargeMax,
    [JsonStringEnumMemberName("river_discharge_min")]
    DischargeMin,
    [JsonStringEnumMemberName("river_discharge_p25")]
    DischargeP25,
    [JsonStringEnumMemberName("river_discharge_p75")]
    DischargeP75,
}
