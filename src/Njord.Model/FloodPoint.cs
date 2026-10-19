namespace Njord.Model;

public sealed record FloodPoint(
    DateTimeOffset ValidFrom,
    double Discharge,
    double? DischargeMean = null,
    double? DischargeMedian = null,
    double? DischargeMax = null,
    double? DischargeMin = null,
    double? DischargeP25 = null,
    double? DischargeP75 = null) : IHasValidTime;
