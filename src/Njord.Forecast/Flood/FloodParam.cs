namespace Njord.Forecast.Flood;

[Flags]
public enum FloodParam
{
    None = 0,
    Discharge = 1 << 0,
    DischargeMean = 1 << 1,
    DischargeMedian = 1 << 2,
    DischargeMax = 1 << 3,
    DischargeMin = 1 << 4,
    DischargeP25 = 1 << 5,
    DischargeP75 = 1 << 6,
}
