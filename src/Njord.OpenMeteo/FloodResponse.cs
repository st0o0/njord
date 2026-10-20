using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public sealed record FloodDailyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("river_discharge")] double?[]? Discharge = null,
    [property: JsonPropertyName("river_discharge_mean")] double?[]? DischargeMean = null,
    [property: JsonPropertyName("river_discharge_median")] double?[]? DischargeMedian = null,
    [property: JsonPropertyName("river_discharge_max")] double?[]? DischargeMax = null,
    [property: JsonPropertyName("river_discharge_min")] double?[]? DischargeMin = null,
    [property: JsonPropertyName("river_discharge_p25")] double?[]? DischargeP25 = null,
    [property: JsonPropertyName("river_discharge_p75")] double?[]? DischargeP75 = null);

public sealed record FloodResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("daily")] FloodDailyData? Daily);
